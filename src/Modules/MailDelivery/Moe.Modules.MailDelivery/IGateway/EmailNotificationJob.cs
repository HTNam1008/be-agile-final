namespace Moe.Modules.MailDelivery.IGateway;

public sealed record EmailNotificationJob
{
    private EmailNotificationJob(
        string notificationType,
        long? personId,
        string? providedEmail,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId)
    {
        NotificationType = notificationType;
        PersonId = personId;
        ProvidedEmail = providedEmail;
        Subject = subject;
        PlainTextBody = plainTextBody;
        HtmlBody = htmlBody;
        EntityType = entityType;
        EntityId = entityId;
    }

    public string NotificationType { get; }

    public long? PersonId { get; }

    public string? ProvidedEmail { get; }

    public string Subject { get; }

    public string PlainTextBody { get; }

    public string? HtmlBody { get; }

    public string? EntityType { get; }

    public string? EntityId { get; }

    public static EmailNotificationJob ForPerson(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody = null,
        string? entityType = null,
        string? entityId = null)
        => new(
            notificationType,
            personId,
            null,
            subject,
            plainTextBody,
            htmlBody,
            entityType,
            entityId);

    public static EmailNotificationJob ForProvidedEmail(
        string notificationType,
        string? providedEmail,
        string subject,
        string plainTextBody,
        string? htmlBody = null,
        string? entityType = null,
        string? entityId = null)
        => new(
            notificationType,
            null,
            providedEmail,
            subject,
            plainTextBody,
            htmlBody,
            entityType,
            entityId);
}
