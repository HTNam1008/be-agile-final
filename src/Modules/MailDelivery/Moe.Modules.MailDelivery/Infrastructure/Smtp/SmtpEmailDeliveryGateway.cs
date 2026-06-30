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
            using MailMessage mail = CreateMailMessage(value, message);
            using SmtpClient client = CreateSmtpClient(value);
            await client.SendMailAsync(mail, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email delivery failed. ToEmail={ToEmail} Subject={Subject}",
                message.ToEmail,
                message.Subject);

            return Result.Failure(MailDeliveryErrors.SendFailed(ex.Message));
        }
    }

    private static MailMessage CreateMailMessage(
        MailDeliveryOptions options,
        EmailDeliveryMessage message)
    {
        MailMessage mail = new()
        {
            From = new MailAddress(options.FromEmail, options.FromDisplayName),
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

    private static SmtpClient CreateSmtpClient(MailDeliveryOptions options)
        => new(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.UserName, options.Password)
        };
}
