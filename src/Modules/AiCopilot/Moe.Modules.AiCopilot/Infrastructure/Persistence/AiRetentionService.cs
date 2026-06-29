using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public sealed class AiRetentionService(
    AiRetentionCleanupRunner cleanupRunner,
    ILogger<AiRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        do
        {
            try
            {
                await cleanupRunner.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "AI retention cleanup failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
