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

    public static readonly Error NotificationNotFound = new(
        "MAIL_DELIVERY.NOTIFICATION_NOT_FOUND",
        "The email notification was not found.");

    public static readonly Error NotificationCannotBeRetried = new(
        "MAIL_DELIVERY.NOTIFICATION_CANNOT_BE_RETRIED",
        "The email notification cannot be retried in its current status.");

    public static readonly Error NotificationCannotBeCancelled = new(
        "MAIL_DELIVERY.NOTIFICATION_CANNOT_BE_CANCELLED",
        "The email notification cannot be cancelled in its current status.");

    public static readonly Error NotificationCannotBeSuppressed = new(
        "MAIL_DELIVERY.NOTIFICATION_CANNOT_BE_SUPPRESSED",
        "The email notification cannot be suppressed in its current status.");
}
