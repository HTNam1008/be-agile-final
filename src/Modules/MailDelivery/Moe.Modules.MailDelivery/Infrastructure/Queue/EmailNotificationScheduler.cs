using Microsoft.Extensions.Logging;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Infrastructure.Queue;

internal sealed class EmailNotificationScheduler(
    IEmailNotificationQueue queue,
    IEmailDeliverySwitch mailSwitch,
    ILogger<EmailNotificationScheduler> logger) : IEmailNotificationScheduler
{
    public bool IsEnabled => mailSwitch.IsEnabled;

    public async Task<bool> EnqueueForPersonAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        if (!mailSwitch.IsEnabled)
        {
            logger.LogInformation(
                "Email notification skipped because MailDelivery is disabled. NotificationType={NotificationType} PersonId={PersonId} EntityType={EntityType} EntityId={EntityId}",
                notificationType,
                personId,
                entityType,
                entityId);
            return false;
        }

        try
        {
            Result result = await queue.EnqueueAsync(
                EmailNotificationJob.ForPerson(
                    notificationType,
                    personId,
                    subject,
                    plainTextBody,
                    htmlBody,
                    entityType,
                    entityId),
                cancellationToken);

            if (result.IsSuccess)
            {
                return true;
            }

            logger.LogWarning(
                "Email notification enqueue failed. NotificationType={NotificationType} PersonId={PersonId} EntityType={EntityType} EntityId={EntityId} ErrorCode={ErrorCode}",
                notificationType,
                personId,
                entityType,
                entityId,
                result.Error.Code);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Email notification threw an exception while enqueueing. NotificationType={NotificationType} PersonId={PersonId} EntityType={EntityType} EntityId={EntityId}",
                notificationType,
                personId,
                entityType,
                entityId);
            return false;
        }
    }
}
