using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

public sealed class EducationAccountLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EducationAccountLifecycleOptions> options,
    ILogger<EducationAccountLifecycleWorker> logger,
    IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Education Account lifecycle worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error while running Education Account lifecycle processing.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    internal async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        EducationAccountLifecycleOptions currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            return;
        }

        if (!TimeOnly.TryParse(currentOptions.RunAtUtc, out TimeOnly runAtUtc))
        {
            logger.LogWarning("Invalid EducationAccountLifecycle:RunAtUtc value {RunAtUtc}.", currentOptions.RunAtUtc);
            return;
        }

        DateTimeOffset now = clock.UtcNow;
        DateOnly today = DateOnly.FromDateTime(now.UtcDateTime);
        TimeOnly currentTime = TimeOnly.FromDateTime(now.UtcDateTime);

        if (currentTime < runAtUtc)
        {
            return;
        }

        await ProcessAsync(
            today,
            now,
            EducationAccountLifecycleRunTriggerTypes.Scheduled,
            cancellationToken);
    }

    internal async Task<EducationAccountLifecycleRunResult> ProcessAsync(
        DateOnly today,
        DateTimeOffset lifecycleAtUtc,
        CancellationToken cancellationToken)
        => await ProcessAsync(
            today,
            lifecycleAtUtc,
            EducationAccountLifecycleRunTriggerTypes.Manual,
            cancellationToken);

    internal async Task<EducationAccountLifecycleRunResult> ProcessAsync(
        DateOnly today,
        DateTimeOffset lifecycleAtUtc,
        string triggerTypeCode,
        CancellationToken cancellationToken)
    {
        using IServiceScope runScope = scopeFactory.CreateScope();
        IEducationAccountLifecycleRunRepository runs =
            runScope.ServiceProvider.GetRequiredService<IEducationAccountLifecycleRunRepository>();
        IUnitOfWork unitOfWork = runScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        if (triggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled
            && await runs.ScheduledRunExistsAsync(today, cancellationToken))
        {
            logger.LogInformation(
                "Scheduled lifecycle run already claimed for {RunDateUtc}, skipping.",
                today);
            return new EducationAccountLifecycleRunResult(0, 0, Skipped: true);
        }

        EducationAccountLifecycleRun run;
        try
        {
            run = EducationAccountLifecycleRun.Start(
                today,
                lifecycleAtUtc,
                triggerTypeCode);
            await runs.AddAsync(run, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            triggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled
            && IsUniqueConstraintViolation(exception))
        {
            logger.LogInformation(
                "Scheduled lifecycle run already claimed for {RunDateUtc}, skipping.",
                today);
            return new EducationAccountLifecycleRunResult(0, 0, Skipped: true);
        }

        AutomaticEducationAccountClosureSummary closureSummary;
        try
        {
            await SendAge30AccountLockRemindersAsync(today, cancellationToken);

            using (IServiceScope closureScope = scopeFactory.CreateScope())
            {
                IAutomaticEducationAccountCloser closer =
                    closureScope.ServiceProvider.GetRequiredService<IAutomaticEducationAccountCloser>();
                closureSummary = await closer.CloseEligibleAsync(today, lifecycleAtUtc, cancellationToken);
            }

            foreach (AutomaticEducationAccountClosureResult result in closureSummary.Results.Where(x => x.Closed))
            {
                run.AddItem(
                    result.PersonId,
                    result.EducationAccountId,
                    EducationAccountLifecycleRunItemActionCodes.Closed,
                    lifecycleAtUtc);
            }

            if (closureSummary.ClosedCount > 0)
            {
                logger.LogInformation(
                    "Closed {ClosedCount} Education Accounts from {ActiveAccountCount} active accounts.",
                    closureSummary.ClosedCount,
                    closureSummary.ActiveAccountCount);
            }

            IReadOnlyCollection<long> candidatePersonIds;

            using (IServiceScope scope = scopeFactory.CreateScope())
            {
                IEligiblePersonLookupGateway people =
                    scope.ServiceProvider.GetRequiredService<IEligiblePersonLookupGateway>();
                candidatePersonIds = await people.FindEligibleForEducationAccountAsync(today, cancellationToken);
            }

            int createdCount = 0;
            foreach (long personId in candidatePersonIds.Distinct())
            {
                using IServiceScope accountScope = scopeFactory.CreateScope();
                IAutomaticEducationAccountCreator creator =
                    accountScope.ServiceProvider.GetRequiredService<IAutomaticEducationAccountCreator>();
                AutomaticEducationAccountCreationResult result =
                    await creator.EnsureCreatedAsync(personId, lifecycleAtUtc, cancellationToken);

                if (result.Created)
                {
                    createdCount++;
                    run.AddItem(
                        personId,
                        result.EducationAccountId,
                        EducationAccountLifecycleRunItemActionCodes.Created,
                        lifecycleAtUtc);
                }
            }

            if (createdCount > 0)
            {
                logger.LogInformation(
                    "Created {CreatedCount} Education Accounts from {CandidateCount} eligible people.",
                    createdCount,
                    candidatePersonIds.Count);
            }

            run.Complete(createdCount, closureSummary.ClosedCount, lifecycleAtUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new EducationAccountLifecycleRunResult(createdCount, closureSummary.ClosedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            run.Fail(exception.Message, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            logger.LogError(
                exception,
                "Education Account lifecycle run failed for {RunDateUtc} with trigger {TriggerTypeCode}.",
                today,
                triggerTypeCode);
            throw;
        }
    }

    private async Task SendAge30AccountLockRemindersAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope reminderScope = scopeFactory.CreateScope();
            IAge30AccountLockReminderEmailService reminderEmails =
                reminderScope.ServiceProvider.GetRequiredService<IAge30AccountLockReminderEmailService>();
            await reminderEmails.SendDueRemindersAsync(today, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Age-30 account lock reminder email processing failed for {RunDateUtc}. Lifecycle processing will continue.",
                today);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException sqlException
           && sqlException.Errors.Cast<SqlError>().Any(error => error.Number is 2601 or 2627);
}
