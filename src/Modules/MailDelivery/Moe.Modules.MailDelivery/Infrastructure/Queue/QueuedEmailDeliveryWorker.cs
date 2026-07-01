using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Infrastructure.Queue;

internal sealed class QueuedEmailDeliveryWorker(
    IEmailNotificationQueue queue,
    IEmailDeliverySwitch mailSwitch,
    IServiceScopeFactory scopeFactory,
    ILogger<QueuedEmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (EmailNotificationJob job in queue.ReadAllAsync(stoppingToken))
        {
            if (!mailSwitch.IsEnabled)
            {
                logger.LogInformation(
                    "Queued email skipped because mail delivery is disabled. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId}",
                    job.NotificationType,
                    job.EntityType,
                    job.EntityId,
                    job.PersonId);
                continue;
            }

            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Queued email job failed unexpectedly. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId}",
                    job.NotificationType,
                    job.EntityType,
                    job.EntityId,
                    job.PersonId);
            }
        }
    }

    private async Task ProcessAsync(
        EmailNotificationJob job,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IEmailRecipientResolver recipientResolver = scope.ServiceProvider.GetRequiredService<IEmailRecipientResolver>();
        IEmailDeliveryGateway deliveryGateway = scope.ServiceProvider.GetRequiredService<IEmailDeliveryGateway>();

        EmailRecipient? recipient = await recipientResolver.ResolveForPersonAsync(job.PersonId, cancellationToken);
        if (recipient is null)
        {
            logger.LogWarning(
                "Queued email skipped because no valid recipient was found. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId}",
                job.NotificationType,
                job.EntityType,
                job.EntityId,
                job.PersonId);
            return;
        }

        Result result = await deliveryGateway.SendAsync(
            new EmailDeliveryMessage(
                recipient.EmailAddress,
                job.Subject,
                job.PlainTextBody,
                job.HtmlBody),
            cancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Queued email delivery failed. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId} RecipientSource={RecipientSource} ErrorCode={ErrorCode}",
                job.NotificationType,
                job.EntityType,
                job.EntityId,
                job.PersonId,
                recipient.SourceCode,
                result.Error.Code);
            return;
        }

        logger.LogInformation(
            "Queued email delivered. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId} RecipientSource={RecipientSource}",
            job.NotificationType,
            job.EntityType,
            job.EntityId,
            job.PersonId,
            recipient.SourceCode);
    }

}
