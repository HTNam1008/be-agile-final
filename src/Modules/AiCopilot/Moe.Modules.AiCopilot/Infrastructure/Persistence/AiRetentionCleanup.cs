using Microsoft.EntityFrameworkCore;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Infrastructure.Persistence;

public static class AiRetentionCleanup
{
    public static async Task<int> CleanupExpiredAsync(MoeDbContext db, DateTime cutoffUtc, CancellationToken ct = default)
    {
        List<AiConversation> expired = await db.Set<AiConversation>().Where(x => x.ExpiresAtUtc < cutoffUtc)
            .Take(500).ToListAsync(ct);

        if (expired.Count == 0) return 0;

        Guid[] expiredIds = expired.Select(x => x.Id).ToArray();

        List<AiReviewRecord> reviews = await db.Set<AiReviewRecord>().Where(x => expiredIds.Contains(x.ConversationId)).ToListAsync(ct);
        List<AdminCenterCase> adminCases = await db.Set<AdminCenterCase>().Where(x => reviews.Select(r => r.Id).Contains(x.ReviewRecordId)).ToListAsync(ct);
        List<AiMessage> messages = await db.Set<AiMessage>().Where(x => expiredIds.Contains(x.ConversationId)).ToListAsync(ct);

        db.Set<AdminCenterCase>().RemoveRange(adminCases);
        db.Set<AiReviewRecord>().RemoveRange(reviews);
        db.Set<AiMessage>().RemoveRange(messages);
        db.Set<AiConversation>().RemoveRange(expired);

        await db.SaveChangesAsync(ct);

        return expiredIds.Length;
    }
}
