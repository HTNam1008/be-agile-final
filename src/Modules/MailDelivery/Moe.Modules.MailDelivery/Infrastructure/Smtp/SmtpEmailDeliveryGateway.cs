using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Infrastructure.Smtp;

internal sealed class SmtpEmailDeliveryGateway(
    IOptions<MailDeliveryOptions> options,
    ILogger<SmtpEmailDeliveryGateway> logger)
    : IEmailDeliveryGateway
{
    public async Task<Result> SendAsync(
        EmailDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        MailDeliveryOptions value = options.Value;

        if (!value.Enabled)
        {
            logger.LogDebug(
                "Email delivery skipped because MailDelivery is disabled. Subject={Subject}",
                message.Subject);
            return Result.Success();
        }

        if (string.IsNullOrWhiteSpace(value.Password))
        {
            return Result.Failure(MailDeliveryErrors.MissingSmtpPassword);
        }

        try
        {
            await SendWithSenderAsync(
                message,
                new SmtpSender(
                    value.UserName,
                    value.Password,
                    value.FromEmail,
                    value.FromDisplayName),
                value,
                cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (value.HasFallbackSender && ShouldRetryWithFallback(ex))
            {
                try
                {
                    await SendWithSenderAsync(
                        message,
                        new SmtpSender(
                            value.FallbackUserName!,
                            value.FallbackPassword!,
                            value.FallbackFromEmail!,
                            value.FallbackFromDisplayName!),
                        value,
                        cancellationToken);

                    logger.LogInformation(
                        "Email delivery succeeded with fallback SMTP sender. Subject={Subject}",
                        message.Subject);
                    return Result.Success();
                }
                catch (Exception fallbackEx) when (fallbackEx is not OperationCanceledException)
                {
                    logger.LogWarning(
                        fallbackEx,
                        "Email delivery fallback failed. Subject={Subject}",
                        message.Subject);
                    return Result.Failure(MailDeliveryErrors.SendFailed(fallbackEx.Message));
                }
            }

            logger.LogWarning(
                ex,
                "Email delivery failed. Subject={Subject}",
                message.Subject);

            return Result.Failure(MailDeliveryErrors.SendFailed(ex.Message));
        }
    }

    private static async Task SendWithSenderAsync(
        EmailDeliveryMessage message,
        SmtpSender sender,
        MailDeliveryOptions options,
        CancellationToken cancellationToken)
    {
        using MailMessage mail = CreateMailMessage(sender, message);
        using SmtpClient client = CreateSmtpClient(options, sender);
        await client.SendMailAsync(mail, cancellationToken);
    }

    private static MailMessage CreateMailMessage(
        SmtpSender sender,
        EmailDeliveryMessage message)
    {
        MailMessage mail = new()
        {
            From = new MailAddress(sender.FromEmail, sender.FromDisplayName),
            Subject = message.Subject,
            Body = message.HtmlBody ?? message.PlainTextBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };

        mail.To.Add(new MailAddress(message.ToEmail));

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.PlainTextBody,
                Encoding.UTF8,
                "text/plain"));
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.HtmlBody,
                Encoding.UTF8,
                "text/html"));
        }

        return mail;
    }

    private static SmtpClient CreateSmtpClient(MailDeliveryOptions options, SmtpSender sender)
        => new(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(sender.UserName, sender.Password)
        };

    private static bool ShouldRetryWithFallback(Exception exception)
    {
        string message = exception.ToString();
        return message.Contains("5.4.5", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Daily user sending limit exceeded", StringComparison.OrdinalIgnoreCase)
            || message.Contains("quota", StringComparison.OrdinalIgnoreCase)
            || message.Contains("limit exceeded", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SmtpSender(
        string UserName,
        string Password,
        string FromEmail,
        string FromDisplayName);
}
