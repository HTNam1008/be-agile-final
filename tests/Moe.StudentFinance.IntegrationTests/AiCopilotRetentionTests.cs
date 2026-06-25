using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.AiCopilot.Infrastructure.Persistence;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotRetentionTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Cleanup_deletes_expired_conversations_and_preserves_active()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime epoch = DateTime.UtcNow;

        Guid expiredId = Guid.NewGuid();
        db.Set<AiConversation>().Add(AiConversation.Start(expiredId, 1, epoch.AddDays(-60)));
        db.Set<AiMessage>().Add(AiMessage.Create(expiredId, "user", "old", epoch.AddDays(-60)));

        Guid activeId = Guid.NewGuid();
        db.Set<AiConversation>().Add(AiConversation.Start(activeId, 1, epoch));
        await db.SaveChangesAsync();

        await AiRetentionCleanup.CleanupExpiredAsync(db, epoch);

        Assert.Null(await db.Set<AiConversation>().FirstOrDefaultAsync(x => x.Id == expiredId));
        Assert.NotNull(await db.Set<AiConversation>().FirstOrDefaultAsync(x => x.Id == activeId));
        Assert.Equal(0, await db.Set<AiMessage>().CountAsync(x => x.ConversationId == expiredId));
    }

    [Fact]
    public async Task Cleanup_cascades_to_review_records_and_admin_cases()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime epoch = DateTime.UtcNow;

        Guid cid = Guid.NewGuid();
        db.Set<AiConversation>().Add(AiConversation.Start(cid, 1, epoch.AddDays(-60)));
        AiReviewRecord review = AiReviewRecord.Create(cid, 1, "MANUAL_FALLBACK", "FAS", null, "transcript", epoch.AddDays(-60));
        db.Set<AiReviewRecord>().Add(review);
        AdminCenterCase acase = AdminCenterCase.Create(review.Id, 1, "test case", "PORTAL", epoch.AddDays(-60));
        db.Set<AdminCenterCase>().Add(acase);
        await db.SaveChangesAsync();

        await AiRetentionCleanup.CleanupExpiredAsync(db, epoch);

        Assert.Equal(0, await db.Set<AiReviewRecord>().CountAsync(x => x.ConversationId == cid));
        Assert.Equal(0, await db.Set<AdminCenterCase>().CountAsync(x => x.Id == acase.Id));
    }

    [Fact]
    public async Task Cleanup_preserves_reviews_for_active_conversations()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime epoch = DateTime.UtcNow;

        Guid cid = Guid.NewGuid();
        db.Set<AiConversation>().Add(AiConversation.Start(cid, 1, epoch));
        AiReviewRecord review = AiReviewRecord.Create(cid, 1, "MANUAL_FALLBACK", "FAS", null, "transcript", epoch);
        db.Set<AiReviewRecord>().Add(review);
        await db.SaveChangesAsync();

        await AiRetentionCleanup.CleanupExpiredAsync(db, epoch);

        Assert.NotNull(await db.Set<AiReviewRecord>().FirstOrDefaultAsync(x => x.Id == review.Id));
    }

    [Fact]
    public async Task Cleanup_handles_empty_database()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        int deleted = await AiRetentionCleanup.CleanupExpiredAsync(db, DateTime.UtcNow);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task Cleanup_respects_cutoff_date()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        DateTime epoch = DateTime.UtcNow;

        Guid futureId = Guid.NewGuid();
        db.Set<AiConversation>().Add(AiConversation.Start(futureId, 1, epoch.AddDays(-60)));
        await db.SaveChangesAsync();

        await AiRetentionCleanup.CleanupExpiredAsync(db, epoch.AddDays(-90));

        Assert.NotNull(await db.Set<AiConversation>().FirstOrDefaultAsync(x => x.Id == futureId));
    }
}
