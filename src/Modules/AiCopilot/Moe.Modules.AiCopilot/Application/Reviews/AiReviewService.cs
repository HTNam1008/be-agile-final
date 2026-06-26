using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Reviews;

public sealed record AiReviewListItem(Guid Id, Guid ConversationId, string Reason, string Domain,
    string Severity, string Status, string? Route, DateTime CreatedAtUtc, bool HasAdminCenterCase);

public sealed record AiReviewDetail(Guid Id, Guid ConversationId, string Reason, string Domain,
    string Severity, string Status, string? Route, string Transcript, DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc, string? CaseDescription, string? ContactPreference);

public sealed class AiReviewService(MoeDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<AiReviewListItem>> List(string? domain, string? reason, string? severity,
        string? status, string? search, int? page, int? pageSize, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        IQueryable<AiReviewRecord> query = db.Set<AiReviewRecord>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(domain)) query = query.Where(x => x.DomainCode == domain);
        if (!string.IsNullOrWhiteSpace(reason)) query = query.Where(x => x.ReasonCode == reason);
        if (!string.IsNullOrWhiteSpace(severity)) query = query.Where(x => x.SeverityCode == severity);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.StatusCode == status);
        if (!string.IsNullOrWhiteSpace(search) && Guid.TryParse(search, out Guid searchId))
            query = query.Where(x => x.ConversationId == searchId);
        if (fromUtc.HasValue) query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.CreatedAtUtc < toUtc.Value);

        int p = Math.Max(1, page ?? 1);
        int ps = Math.Clamp(pageSize ?? 20, 1, 200);

        return await query.OrderByDescending(x => x.CreatedAtUtc)
            .Skip((p - 1) * ps).Take(ps)
            .Select(x => new AiReviewListItem(x.Id, x.ConversationId, x.ReasonCode, x.DomainCode,
                x.SeverityCode, x.StatusCode, x.Route, x.CreatedAtUtc,
                db.Set<AdminCenterCase>().Any(c => c.ReviewRecordId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<AiReviewDetail?> Get(Guid id, CancellationToken cancellationToken)
        => await db.Set<AiReviewRecord>().AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AiReviewDetail(x.Id, x.ConversationId, x.ReasonCode, x.DomainCode,
                x.SeverityCode, x.StatusCode, x.Route, x.TranscriptRedacted, x.CreatedAtUtc,
                x.ResolvedAtUtc,
                db.Set<AdminCenterCase>().Where(c => c.ReviewRecordId == x.Id).Select(c => c.DescriptionRedacted).FirstOrDefault(),
                db.Set<AdminCenterCase>().Where(c => c.ReviewRecordId == x.Id).Select(c => c.ContactPreferenceCode).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> Resolve(Guid id, CancellationToken cancellationToken)
    {
        AiReviewRecord? review = await db.Set<AiReviewRecord>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (review is null) return false;
        if (review.StatusCode != "RESOLVED")
        {
            review.Resolve(currentUser.UserAccountId ?? throw new InvalidOperationException("Admin account is required."), DateTime.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }
}
