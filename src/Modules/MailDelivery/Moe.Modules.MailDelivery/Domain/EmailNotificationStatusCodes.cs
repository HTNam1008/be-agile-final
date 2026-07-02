namespace Moe.Modules.MailDelivery.Domain;

public static class EmailNotificationStatusCodes
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Sent = "SENT";
    public const string FailedRetryable = "FAILED_RETRYABLE";
    public const string FailedFinal = "FAILED_FINAL";
    public const string Cancelled = "CANCELLED";
    public const string Suppressed = "SUPPRESSED";

    public static readonly string[] Queueable =
    [
        Pending,
        FailedRetryable
    ];
}
