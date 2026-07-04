namespace Moe.Modules.Notifications.Domain.Notifications;

public sealed class NotificationRealtimeDelivery
{
    private NotificationRealtimeDelivery() { }

    private NotificationRealtimeDelivery(
        Notification notification,
        long recipientUserAccountId,
        DateTime createdAtUtc,
        int maxAttempts)
    {
        Notification = notification;
        RecipientUserAccountId = recipientUserAccountId;
        StatusCode = NotificationRealtimeDeliveryStatusCodes.Pending;
        AttemptCount = 0;
        MaxAttempts = Math.Max(1, maxAttempts);
        NextAttemptAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }

    public long NotificationId { get; private set; }

    public Notification Notification { get; private set; } = null!;

    public long RecipientUserAccountId { get; private set; }

    public string StatusCode { get; private set; } = NotificationRealtimeDeliveryStatusCodes.Pending;

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTime NextAttemptAtUtc { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? DeliveredAtUtc { get; private set; }

    public static NotificationRealtimeDelivery Create(
        Notification notification,
        long recipientUserAccountId,
        DateTime createdAtUtc,
        int maxAttempts)
        => new(notification, recipientUserAccountId, createdAtUtc, maxAttempts);

    public void MarkProcessing(DateTime lockedUntilUtc)
    {
        StatusCode = NotificationRealtimeDeliveryStatusCodes.Processing;
        LockedUntilUtc = lockedUntilUtc;
    }

    public void MarkDelivered(DateTime deliveredAtUtc)
    {
        StatusCode = NotificationRealtimeDeliveryStatusCodes.Delivered;
        DeliveredAtUtc = deliveredAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = null;
        LastErrorMessage = null;
    }

    public void MarkFailure(
        string errorCode,
        string errorMessage,
        DateTime failedAtUtc,
        DateTime? retryAtUtc)
    {
        AttemptCount++;
        LockedUntilUtc = null;
        LastErrorCode = errorCode;
        LastErrorMessage = Truncate(errorMessage, 1000);

        if (retryAtUtc.HasValue && AttemptCount < MaxAttempts)
        {
            StatusCode = NotificationRealtimeDeliveryStatusCodes.FailedRetryable;
            NextAttemptAtUtc = retryAtUtc.Value;
            return;
        }

        StatusCode = NotificationRealtimeDeliveryStatusCodes.FailedFinal;
        NextAttemptAtUtc = failedAtUtc;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
