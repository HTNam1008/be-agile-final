using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;

namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle;

public sealed class EducationAccountLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EducationAccountLifecycleOptions> options,
    ILogger<EducationAccountLifecycleWorker> logger,
    IClock clock) : BackgroundService
{
    private DateOnly? _lastRunDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Education Account lifecycle worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error while running Education Account lifecycle processing.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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

        if (_lastRunDate == today || currentTime < runAtUtc)
        {
            return;
        }

        await ProcessAsync(today, now, cancellationToken);
        _lastRunDate = today;
    }

    internal async Task ProcessAsync(
        DateOnly today,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> candidatePersonIds;

        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            IEligiblePersonLookupGateway people =
                scope.ServiceProvider.GetRequiredService<IEligiblePersonLookupGateway>();
            candidatePersonIds = await people.FindEligibleForEducationAccountAsync(today, cancellationToken);
        }

        if (candidatePersonIds.Count == 0)
        {
            return;
        }

        int createdCount = 0;
        foreach (long personId in candidatePersonIds.Distinct())
        {
            using IServiceScope accountScope = scopeFactory.CreateScope();
            IAutomaticEducationAccountCreator creator =
                accountScope.ServiceProvider.GetRequiredService<IAutomaticEducationAccountCreator>();
            AutomaticEducationAccountCreationResult result =
                await creator.EnsureCreatedAsync(personId, openedAtUtc, cancellationToken);

            if (result.Created)
            {
                createdCount++;
            }
        }

        if (createdCount > 0)
        {
            logger.LogInformation(
                "Created {CreatedCount} Education Accounts from {CandidateCount} eligible people.",
                createdCount,
                candidatePersonIds.Count);
        }
    }
}
