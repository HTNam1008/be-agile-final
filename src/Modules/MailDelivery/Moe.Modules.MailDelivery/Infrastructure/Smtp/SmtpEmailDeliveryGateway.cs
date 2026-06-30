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

        SmtpAccount primaryAccount = SmtpAccount.Primary(value);

        try
        {
            await SendWithAccountAsync(value, primaryAccount, message, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            && IsQuotaFailure(ex)
            && SmtpAccount.TryFallback(value) is SmtpAccount fallbackAccount)
        {
            logger.LogWarning(
                ex,
                "Primary email account quota was exceeded. Retrying with fallback account. ToEmail={ToEmail} Subject={Subject}",
                message.ToEmail,
                message.Subject);

            try
            {
                await SendWithAccountAsync(value, fallbackAccount, message, cancellationToken);
                return Result.Success();
            }
            catch (Exception fallbackException) when (fallbackException is not OperationCanceledException)
            {
                logger.LogWarning(
                    fallbackException,
                    "Fallback email delivery failed. ToEmail={ToEmail} Subject={Subject}",
                    message.ToEmail,
                    message.Subject);

                return Result.Failure(MailDeliveryErrors.SendFailed(fallbackException.Message));
            }
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

    private static async Task SendWithAccountAsync(
        MailDeliveryOptions options,
        SmtpAccount account,
        EmailDeliveryMessage message,
        CancellationToken cancellationToken)
    {
        using MailMessage mail = CreateMailMessage(account, message);
        using SmtpClient client = CreateSmtpClient(options, account);
        await client.SendMailAsync(mail, cancellationToken);
    }

    private static MailMessage CreateMailMessage(
        SmtpAccount account,
        EmailDeliveryMessage message)
    {
        MailMessage mail = new()
        {
            From = new MailAddress(account.FromEmail, account.FromDisplayName),
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

    private static SmtpClient CreateSmtpClient(MailDeliveryOptions options, SmtpAccount account)
        => new(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(account.UserName, account.Password)
        };

    private static bool IsQuotaFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            string message = current.Message;
            if (message.Contains("5.4.5", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Daily user sending limit exceeded", StringComparison.OrdinalIgnoreCase)
                || message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record SmtpAccount(
        string UserName,
        string Password,
        string FromEmail,
        string FromDisplayName)
    {
        public static SmtpAccount Primary(MailDeliveryOptions options)
            => new(
                options.UserName,
                NormalizePassword(options.Password),
                options.FromEmail,
                options.FromDisplayName);

        public static SmtpAccount? TryFallback(MailDeliveryOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FallbackUserName)
                || string.IsNullOrWhiteSpace(options.FallbackPassword)
                || string.IsNullOrWhiteSpace(options.FallbackFromEmail))
            {
                return null;
            }

            return new SmtpAccount(
                options.FallbackUserName,
                NormalizePassword(options.FallbackPassword),
                options.FallbackFromEmail,
                string.IsNullOrWhiteSpace(options.FallbackFromDisplayName)
                    ? options.FromDisplayName
                    : options.FallbackFromDisplayName);
        }

        private static string NormalizePassword(string password)
            => string.Concat(password.Where(character => !char.IsWhiteSpace(character)));
    }
}
