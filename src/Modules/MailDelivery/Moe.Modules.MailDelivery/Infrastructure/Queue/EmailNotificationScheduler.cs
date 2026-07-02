using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.MailDelivery.Infrastructure.Queue;

internal sealed class EmailNotificationScheduler(
    MoeDbContext dbContext,
    IEmailDeliverySwitch mailSwitch,
    IClock clock,
    IOptions<MailDeliveryOptions> options,
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
            DateTime nowUtc = clock.UtcNow.UtcDateTime;
            EmailNotification notification = EmailNotification.Create(
                notificationType,
                personId,
                subject,
                plainTextBody,
                htmlBody,
                entityType,
                entityId,
                nowUtc,
                options.Value.Worker.MaxAttempts);

            dbContext.Set<EmailNotification>().Add(notification);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Email notification scheduled. EmailNotificationId={EmailNotificationId} NotificationType={NotificationType} PersonId={PersonId} EntityType={EntityType} EntityId={EntityId}",
                notification.Id,
                notificationType,
                personId,
                entityType,
                entityId);

            return true;
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
