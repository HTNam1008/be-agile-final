using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public sealed class AiRetentionService(IServiceScopeFactory scopeFactory, ILogger<AiRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        do
        {
            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
                int count = await AiRetentionCleanup.CleanupExpiredAsync(db, DateTime.UtcNow, stoppingToken);
                if (count > 0) logger.LogInformation("Deleted {Count} expired AI conversations.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "AI retention cleanup failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
