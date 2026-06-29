using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public sealed class AiRetentionCleanupRunner(
    IServiceScopeFactory scopeFactory,
    IClock clock,
    ILogger<AiRetentionCleanupRunner> logger)
{
    public async Task<AiRetentionCleanupResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime cutoffUtc = clock.UtcNow.UtcDateTime;
        int deletedCount = await AiRetentionCleanup.CleanupExpiredAsync(db, cutoffUtc, cancellationToken);
        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {Count} expired AI conversations.", deletedCount);
        }

        return new AiRetentionCleanupResult(deletedCount, cutoffUtc);
    }
}

public sealed record AiRetentionCleanupResult(int DeletedCount, DateTime CutoffUtc);
