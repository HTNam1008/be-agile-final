namespace Moe.Modules.MailDelivery.Domain;

public sealed class EmailNotification
{
    private EmailNotification() { }

    private EmailNotification(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId,
        DateTime createdAtUtc,
        int maxAttempts)
    {
        NotificationType = notificationType;
        PersonId = personId;
        Subject = subject;
        PlainTextBody = plainTextBody;
        HtmlBody = htmlBody;
        EntityType = entityType;
        EntityId = entityId;
        StatusCode = EmailNotificationStatusCodes.Pending;
        Priority = 0;
        AttemptCount = 0;
        MaxAttempts = maxAttempts;
        NextAttemptAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public long Id { get; private set; }

    public string NotificationType { get; private set; } = string.Empty;

    public long PersonId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string PlainTextBody { get; private set; } = string.Empty;

    public string? HtmlBody { get; private set; }

    public string? EntityType { get; private set; }

    public string? EntityId { get; private set; }

    public string StatusCode { get; private set; } = EmailNotificationStatusCodes.Pending;

    public int Priority { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTime NextAttemptAtUtc { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public string? LastErrorCode { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public string? ResolvedToEmailMasked { get; private set; }

    public string? RecipientSourceCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SentAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public static EmailNotification Create(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId,
        DateTime createdAtUtc,
        int maxAttempts)
        => new(
            notificationType,
            personId,
            subject,
            plainTextBody,
            htmlBody,
            entityType,
            entityId,
            createdAtUtc,
            Math.Max(1, maxAttempts));

    public void MarkProcessing(DateTime lockedUntilUtc)
    {
        StatusCode = EmailNotificationStatusCodes.Processing;
        LockedUntilUtc = lockedUntilUtc;
    }

    public void MarkSent(string resolvedToEmailMasked, string recipientSourceCode, DateTime sentAtUtc)
    {
        StatusCode = EmailNotificationStatusCodes.Sent;
        ResolvedToEmailMasked = resolvedToEmailMasked;
        RecipientSourceCode = recipientSourceCode;
        SentAtUtc = sentAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = null;
        LastErrorMessage = null;
    }

    public void MarkFinalFailure(string errorCode, string errorMessage, DateTime failedAtUtc)
    {
        StatusCode = EmailNotificationStatusCodes.FailedFinal;
        AttemptCount++;
        NextAttemptAtUtc = failedAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = errorCode;
        LastErrorMessage = Truncate(errorMessage, 1000);
    }

    public void MarkRetryableFailure(string errorCode, string errorMessage, DateTime nextAttemptAtUtc)
    {
        StatusCode = EmailNotificationStatusCodes.FailedRetryable;
        AttemptCount++;
        NextAttemptAtUtc = nextAttemptAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = errorCode;
        LastErrorMessage = Truncate(errorMessage, 1000);
    }

    public void MarkCancelled(DateTime cancelledAtUtc)
    {
        StatusCode = EmailNotificationStatusCodes.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        LockedUntilUtc = null;
    }

    public void MarkSuppressed(DateTime suppressedAtUtc, string? reason)
    {
        StatusCode = EmailNotificationStatusCodes.Suppressed;
        CancelledAtUtc = suppressedAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = "MAIL_DELIVERY.SUPPRESSED";
        LastErrorMessage = Truncate(reason ?? "Suppressed by administrator.", 1000);
    }

    public void ResetForRetry(DateTime retryAtUtc)
    {
        StatusCode = EmailNotificationStatusCodes.Pending;
        NextAttemptAtUtc = retryAtUtc;
        LockedUntilUtc = null;
        LastErrorCode = null;
        LastErrorMessage = null;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
