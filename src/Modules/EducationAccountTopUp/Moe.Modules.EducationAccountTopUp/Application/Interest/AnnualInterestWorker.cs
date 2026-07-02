using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;

namespace Moe.Modules.EducationAccountTopUp.Application.Interest;

internal sealed class AnnualInterestWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EducationAccountInterestOptions> options,
    ILogger<AnnualInterestWorker> logger,
    IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Education Account annual interest worker started");

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
                logger.LogError(exception, "Unhandled error while processing annual education account interest.");
            }

            await DelayUntilNextDailyCheckAsync(stoppingToken);
        }
    }

    internal async Task<AnnualInterestProcessingResult?> RunIfDueAsync(CancellationToken cancellationToken)
    {
        EducationAccountInterestOptions currentOptions = options.Value;
        if (!currentOptions.Enabled)
        {
            return null;
        }

        if (!TimeOnly.TryParse(currentOptions.RunAtUtc, out TimeOnly runAtUtc))
        {
            logger.LogWarning("Invalid EducationAccountInterest:RunAtUtc value {RunAtUtc}.", currentOptions.RunAtUtc);
            return null;
        }

        DateTimeOffset now = clock.UtcNow;
        TimeOnly currentTimeUtc = TimeOnly.FromDateTime(now.UtcDateTime);
        if (currentTimeUtc < runAtUtc)
        {
            return null;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        IAnnualInterestProcessor processor =
            scope.ServiceProvider.GetRequiredService<IAnnualInterestProcessor>();

        return await processor.ProcessDueInterestAsync(
            clock.TodayInSingapore(),
            now,
            cancellationToken);
    }

    private async Task DelayUntilNextDailyCheckAsync(CancellationToken stoppingToken)
    {
        DateTime nowUtc = clock.UtcNow.UtcDateTime;
        DateTime nextCheckUtc = nowUtc.Date.AddDays(1);
        TimeSpan delay = nextCheckUtc - nowUtc;
        if (delay <= TimeSpan.Zero)
        {
            delay = TimeSpan.FromMinutes(5);
        }

        await Task.Delay(delay, stoppingToken);
    }
}
