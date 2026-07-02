namespace Moe.Modules.MailDelivery.IGateway;

public interface IEmailNotificationScheduler
{
    bool IsEnabled { get; }

    Task<bool> EnqueueForPersonAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken);
}
