using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Domain;
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
                DateTime now = DateTime.UtcNow;
                Guid[] expired = await db.Set<AiConversation>().Where(x => x.ExpiresAtUtc < now)
                    .Select(x => x.Id).Take(500).ToArrayAsync(stoppingToken);
                if (expired.Length > 0)
                {
                    Guid[] reviews = await db.Set<AiReviewRecord>().Where(x => expired.Contains(x.ConversationId)).Select(x => x.Id).ToArrayAsync(stoppingToken);
                    await db.Set<AdminCenterCase>().Where(x => reviews.Contains(x.ReviewRecordId)).ExecuteDeleteAsync(stoppingToken);
                    await db.Set<AiReviewRecord>().Where(x => expired.Contains(x.ConversationId)).ExecuteDeleteAsync(stoppingToken);
                    await db.Set<AiMessage>().Where(x => expired.Contains(x.ConversationId)).ExecuteDeleteAsync(stoppingToken);
                    await db.Set<AiConversation>().Where(x => expired.Contains(x.Id)).ExecuteDeleteAsync(stoppingToken);
                    logger.LogInformation("Deleted {Count} expired AI conversations.", expired.Length);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "AI retention cleanup failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
