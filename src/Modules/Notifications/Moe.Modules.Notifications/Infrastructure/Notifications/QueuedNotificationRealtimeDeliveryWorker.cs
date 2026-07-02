using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.Notifications.Application;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.Notifications.Infrastructure.Notifications;

public sealed class QueuedNotificationRealtimeDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationRealtimeOptions> options,
    ILogger<QueuedNotificationRealtimeDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NotificationRealtimeOptions currentOptions = options.Value;

            if (!currentOptions.Enabled || !currentOptions.Worker.Enabled)
            {
                await DelayAsync(currentOptions.Worker, stoppingToken);
                continue;
            }

            try
            {
                int processed = await ProcessPendingBatchAsync(currentOptions.Worker, stoppingToken);
                if (processed == 0)
                {
                    await DelayAsync(currentOptions.Worker, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Queued realtime notification worker failed unexpectedly.");
                await DelayAsync(currentOptions.Worker, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessPendingBatchAsync(
        NotificationRealtimeWorkerOptions workerOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        DateTime nowUtc = clock.UtcNow.UtcDateTime;
        int batchSize = Math.Clamp(workerOptions.BatchSize, 1, 100);

        List<NotificationRealtimeDelivery> deliveries = await dbContext.Set<NotificationRealtimeDelivery>()
            .Where(delivery =>
                (NotificationRealtimeDeliveryStatusCodes.Queueable.Contains(delivery.StatusCode)
                    && delivery.NextAttemptAtUtc <= nowUtc)
                || (delivery.StatusCode == NotificationRealtimeDeliveryStatusCodes.Processing
                    && delivery.LockedUntilUtc <= nowUtc))
            .OrderBy(delivery => delivery.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (NotificationRealtimeDelivery delivery in deliveries)
        {
            delivery.MarkProcessing(nowUtc.AddSeconds(Math.Max(30, workerOptions.LockSeconds)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (NotificationRealtimeDelivery delivery in deliveries)
        {
            await ProcessAsync(scope.ServiceProvider, delivery, workerOptions, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return deliveries.Count;
    }

    private async Task ProcessAsync(
        IServiceProvider services,
        NotificationRealtimeDelivery delivery,
        NotificationRealtimeWorkerOptions workerOptions,
        CancellationToken cancellationToken)
    {
        MoeDbContext dbContext = services.GetRequiredService<MoeDbContext>();
        IClock clock = services.GetRequiredService<IClock>();
        INotificationRealtimeNotifier realtimeNotifier = services.GetRequiredService<INotificationRealtimeNotifier>();

        Notification? notification = await dbContext.Set<Notification>()
            .FindAsync([delivery.NotificationId], cancellationToken);

        if (notification is null)
        {
            DateTime failedAtUtc = clock.UtcNow.UtcDateTime;
            delivery.MarkFailure(
                "NOTIFICATIONS.NOTIFICATION_NOT_FOUND",
                "Notification inbox record could not be found.",
                failedAtUtc,
                retryAtUtc: null);

            logger.LogError(
                "Realtime notification delivery failed because the notification record was missing. DeliveryId={DeliveryId} NotificationId={NotificationId} RecipientUserAccountId={RecipientUserAccountId}",
                delivery.Id,
                delivery.NotificationId,
                delivery.RecipientUserAccountId);
            return;
        }

        try
        {
            await realtimeNotifier.NotifyUserAccountAsync(
                delivery.RecipientUserAccountId,
                new NotificationRealtimeMessage(
                    notification.Id,
                    notification.NotificationTypeCode,
                    notification.Title,
                    notification.Body),
                cancellationToken);

            delivery.MarkDelivered(clock.UtcNow.UtcDateTime);

            logger.LogInformation(
                "Realtime notification delivered. DeliveryId={DeliveryId} NotificationId={NotificationId} RecipientUserAccountId={RecipientUserAccountId}",
                delivery.Id,
                delivery.NotificationId,
                delivery.RecipientUserAccountId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            DateTime failedAtUtc = clock.UtcNow.UtcDateTime;
            delivery.MarkFailure(
                "NOTIFICATIONS.REALTIME_SEND_FAILED",
                exception.Message,
                failedAtUtc,
                CalculateNextAttemptAtUtc(failedAtUtc, delivery.AttemptCount + 1, workerOptions));

            logger.LogError(
                exception,
                "Realtime notification delivery failed. DeliveryId={DeliveryId} NotificationId={NotificationId} RecipientUserAccountId={RecipientUserAccountId} AttemptCount={AttemptCount} StatusCode={StatusCode}",
                delivery.Id,
                delivery.NotificationId,
                delivery.RecipientUserAccountId,
                delivery.AttemptCount,
                delivery.StatusCode);
        }
    }

    private static Task DelayAsync(
        NotificationRealtimeWorkerOptions workerOptions,
        CancellationToken cancellationToken)
        => Task.Delay(
            TimeSpan.FromSeconds(Math.Clamp(workerOptions.PollIntervalSeconds, 1, 300)),
            cancellationToken);

    private static DateTime CalculateNextAttemptAtUtc(
        DateTime failedAtUtc,
        int failedAttemptCount,
        NotificationRealtimeWorkerOptions workerOptions)
    {
        if (failedAttemptCount >= Math.Max(1, workerOptions.MaxAttempts))
        {
            return failedAtUtc;
        }

        int delaySeconds = failedAttemptCount switch
        {
            <= 1 => 10,
            2 => 30,
            3 => 60,
            _ => 300
        };

        return failedAtUtc.AddSeconds(delaySeconds);
    }
}
