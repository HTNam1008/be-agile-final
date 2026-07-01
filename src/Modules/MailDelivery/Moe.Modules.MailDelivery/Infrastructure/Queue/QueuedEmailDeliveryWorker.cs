using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.MailDelivery.Infrastructure.Queue;

internal sealed class QueuedEmailDeliveryWorker(
    IEmailDeliverySwitch mailSwitch,
    IServiceScopeFactory scopeFactory,
    IOptions<MailDeliveryOptions> options,
    ILogger<QueuedEmailDeliveryWorker> logger) : BackgroundService
{
    private readonly object _rateLimitLock = new();
    private DateTime _rateWindowStartedAtUtc = DateTime.MinValue;
    private int _sentInWindow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            MailDeliveryWorkerOptions workerOptions = options.Value.Worker;

            if (!options.Value.Enabled || !workerOptions.Enabled || !mailSwitch.IsEnabled)
            {
                await DelayAsync(workerOptions, stoppingToken);
                continue;
            }

            try
            {
                int processed = await ProcessPendingBatchAsync(workerOptions, stoppingToken);
                if (processed == 0)
                {
                    await DelayAsync(workerOptions, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Queued email worker failed unexpectedly.");
                await DelayAsync(workerOptions, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessPendingBatchAsync(
        MailDeliveryWorkerOptions workerOptions,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        IClock clock = scope.ServiceProvider.GetRequiredService<IClock>();

        DateTime nowUtc = clock.UtcNow.UtcDateTime;
        int remainingRateLimit = GetRemainingRateLimit(nowUtc, workerOptions);
        if (remainingRateLimit <= 0)
        {
            return 0;
        }

        int batchSize = Math.Clamp(workerOptions.BatchSize, 1, 100);
        int effectiveBatchSize = Math.Min(batchSize, remainingRateLimit);

        List<EmailNotification> jobs = await dbContext.Set<EmailNotification>()
            .Where(job =>
                (EmailNotificationStatusCodes.Queueable.Contains(job.StatusCode)
                    && job.NextAttemptAtUtc <= nowUtc)
                || (job.StatusCode == EmailNotificationStatusCodes.Processing
                    && job.LockedUntilUtc <= nowUtc))
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.CreatedAtUtc)
            .Take(effectiveBatchSize)
            .ToListAsync(cancellationToken);

        foreach (EmailNotification job in jobs)
        {
            job.MarkProcessing(nowUtc.AddSeconds(Math.Max(30, workerOptions.LockSeconds)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (EmailNotification job in jobs)
        {
            bool smtpAttempted = await ProcessAsync(scope.ServiceProvider, job, workerOptions, cancellationToken);
            if (smtpAttempted)
            {
                RegisterSendAttempt(clock.UtcNow.UtcDateTime, workerOptions);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return jobs.Count;
    }

    private async Task<bool> ProcessAsync(
        IServiceProvider services,
        EmailNotification job,
        MailDeliveryWorkerOptions workerOptions,
        CancellationToken cancellationToken)
    {
        IEmailRecipientResolver recipientResolver = services.GetRequiredService<IEmailRecipientResolver>();
        IEmailDeliveryGateway deliveryGateway = services.GetRequiredService<IEmailDeliveryGateway>();
        IClock clock = services.GetRequiredService<IClock>();

        EmailRecipient? recipient = await recipientResolver.ResolveForPersonAsync(job.PersonId, cancellationToken);
        if (recipient is null)
        {
            logger.LogWarning(
                "Queued email skipped because no valid recipient was found. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId}",
                job.NotificationType,
                job.EntityType,
                job.EntityId,
                job.PersonId);
            job.MarkFinalFailure(
                "MAIL_DELIVERY.RECIPIENT_NOT_FOUND",
                "No valid recipient was found.",
                clock.UtcNow.UtcDateTime);
            return false;
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
            DateTime failedAtUtc = clock.UtcNow.UtcDateTime;
            logger.LogWarning(
                "Queued email delivery failed. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId} RecipientSource={RecipientSource} ErrorCode={ErrorCode}",
                job.NotificationType,
                job.EntityType,
                job.EntityId,
                job.PersonId,
                recipient.SourceCode,
                result.Error.Code);

            if (IsRetryable(result.Error) && job.AttemptCount + 1 < Math.Max(1, job.MaxAttempts))
            {
                job.MarkRetryableFailure(
                    result.Error.Code,
                    result.Error.Message,
                    CalculateNextAttemptAtUtc(failedAtUtc, job.AttemptCount + 1));
            }
            else
            {
                job.MarkFinalFailure(result.Error.Code, result.Error.Message, failedAtUtc);
            }

            return true;
        }

        job.MarkSent(MaskEmail(recipient.EmailAddress), recipient.SourceCode, clock.UtcNow.UtcDateTime);

        logger.LogInformation(
            "Queued email delivered. NotificationType={NotificationType} EntityType={EntityType} EntityId={EntityId} PersonId={PersonId} RecipientSource={RecipientSource}",
            job.NotificationType,
            job.EntityType,
            job.EntityId,
            job.PersonId,
            recipient.SourceCode);

        return true;
    }

    private static Task DelayAsync(MailDeliveryWorkerOptions workerOptions, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(Math.Clamp(workerOptions.PollIntervalSeconds, 1, 300)), cancellationToken);

    private int GetRemainingRateLimit(DateTime nowUtc, MailDeliveryWorkerOptions workerOptions)
    {
        int maxEmailsPerMinute = workerOptions.MaxEmailsPerMinute;
        if (maxEmailsPerMinute <= 0)
        {
            return int.MaxValue;
        }

        lock (_rateLimitLock)
        {
            ResetRateWindowIfNeeded(nowUtc);
            return Math.Max(0, maxEmailsPerMinute - _sentInWindow);
        }
    }

    private void RegisterSendAttempt(DateTime nowUtc, MailDeliveryWorkerOptions workerOptions)
    {
        if (workerOptions.MaxEmailsPerMinute <= 0)
        {
            return;
        }

        lock (_rateLimitLock)
        {
            ResetRateWindowIfNeeded(nowUtc);
            _sentInWindow++;
        }
    }

    private void ResetRateWindowIfNeeded(DateTime nowUtc)
    {
        if (_rateWindowStartedAtUtc == DateTime.MinValue
            || nowUtc - _rateWindowStartedAtUtc >= TimeSpan.FromMinutes(1))
        {
            _rateWindowStartedAtUtc = nowUtc;
            _sentInWindow = 0;
        }
    }

    private static DateTime CalculateNextAttemptAtUtc(DateTime failedAtUtc, int failedAttemptCount)
    {
        int delayMinutes = failedAttemptCount switch
        {
            <= 1 => 1,
            2 => 5,
            3 => 15,
            _ => 30
        };

        return failedAtUtc.AddMinutes(delayMinutes);
    }

    private static bool IsRetryable(Error error)
    {
        if (string.Equals(error.Code, MailDeliveryErrors.MissingSmtpPassword.Code, StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(error.Code, "MAIL_DELIVERY.SEND_FAILED", StringComparison.Ordinal);
    }

    private static string MaskEmail(string email)
    {
        int atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[atIndex..]}";
    }
}
