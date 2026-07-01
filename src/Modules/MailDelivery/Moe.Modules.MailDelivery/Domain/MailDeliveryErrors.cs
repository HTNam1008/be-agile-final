using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Domain;

public static class MailDeliveryErrors
{
    public static readonly Error MissingSmtpPassword = new(
        "MAIL_DELIVERY.SMTP_PASSWORD_MISSING",
        "SMTP password is not configured.");

    public static Error SendFailed(string reason)
        => new(
            "MAIL_DELIVERY.SEND_FAILED",
            string.IsNullOrWhiteSpace(reason)
                ? "The email could not be sent."
                : $"The email could not be sent. {reason}");

    public static readonly Error QueueFull = new(
        "MAIL_DELIVERY.QUEUE_FULL",
        "The email notification queue is full.");
}
