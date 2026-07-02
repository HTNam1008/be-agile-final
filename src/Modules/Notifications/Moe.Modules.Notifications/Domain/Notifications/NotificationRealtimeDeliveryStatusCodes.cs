namespace Moe.Modules.Notifications.Domain.Notifications;

public static class NotificationRealtimeDeliveryStatusCodes
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Delivered = "DELIVERED";
    public const string FailedRetryable = "FAILED_RETRYABLE";
    public const string FailedFinal = "FAILED_FINAL";

    public static readonly string[] Queueable =
    [
        Pending,
        FailedRetryable
    ];
}
