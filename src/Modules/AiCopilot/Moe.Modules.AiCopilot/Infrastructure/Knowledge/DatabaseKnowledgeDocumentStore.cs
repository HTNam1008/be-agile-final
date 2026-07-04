using Microsoft.EntityFrameworkCore;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class DatabaseKnowledgeDocumentStore : IKnowledgeDocumentStore
{
    private readonly MoeDbContext _db;

    private bool _seeded;

    public DatabaseKnowledgeDocumentStore(MoeDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> GetAllAsync(CancellationToken ct)
    {
        await SeedIfEmptyAsync(ct);
        List<AiKnowledgeDocument> entities = await _db.Set<AiKnowledgeDocument>()
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        return entities.Select(MapToDocument).ToList();
    }

    private async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        if (_seeded) return;
        bool empty = !await _db.Set<AiKnowledgeDocument>().AnyAsync(ct);
        if (!empty) { _seeded = true; return; }

        var embedded = new EmbeddedKnowledgeDocumentStore();
        IReadOnlyList<KnowledgeDocument> docs = await embedded.GetAllAsync(ct);
        DateTime now = DateTime.UtcNow;
        foreach (KnowledgeDocument doc in docs)
        {
            var entity = AiKnowledgeDocument.Create(doc.Id, doc.Title, doc.Domain, doc.Content, now);
            entity.Section = doc.Section;
            entity.Status = doc.Status;
            entity.Version = doc.Version;
            entity.EffectiveDate = doc.EffectiveDate;
            entity.Url = doc.Url;
            entity.AlwaysInclude = doc.AlwaysInclude;
            entity.ReviewOwner = doc.ReviewOwner;
            entity.Synonyms = doc.Synonyms;
            entity.AllowedIntents = doc.AllowedIntents;
            entity.FollowUps = doc.FollowUps;
            _db.Set<AiKnowledgeDocument>().Add(entity);
        }
        await _db.SaveChangesAsync(ct);
        _seeded = true;
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> GetByDomainAsync(string domain, CancellationToken ct)
    {
        await SeedIfEmptyAsync(ct);
        List<AiKnowledgeDocument> entities = await _db.Set<AiKnowledgeDocument>()
            .Where(x => x.Domain == domain.ToUpperInvariant())
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        return entities.Select(MapToDocument).ToList();
    }

    public async Task UpsertAsync(KnowledgeDocument document, CancellationToken ct)
    {
        AiKnowledgeDocument? existing = await _db.Set<AiKnowledgeDocument>()
            .FirstOrDefaultAsync(x => x.Id == document.Id, ct);

        DateTime now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = AiKnowledgeDocument.Create(document.Id, document.Title, document.Domain, document.Content, now);
            existing.Section = document.Section;
            existing.Status = document.Status;
            existing.Version = document.Version;
            existing.EffectiveDate = document.EffectiveDate;
            existing.Url = document.Url;
            existing.AlwaysInclude = document.AlwaysInclude;
            existing.ReviewOwner = document.ReviewOwner;
            existing.Synonyms = document.Synonyms;
            existing.AllowedIntents = document.AllowedIntents;
            existing.FollowUps = document.FollowUps;
            _db.Set<AiKnowledgeDocument>().Add(existing);
        }
        else
        {
            existing.Section = document.Section;
            existing.Domain = document.Domain.ToUpperInvariant();
            existing.Status = document.Status;
            existing.Version = document.Version;
            existing.EffectiveDate = document.EffectiveDate;
            existing.Content = document.Content;
            existing.Url = document.Url;
            existing.AlwaysInclude = document.AlwaysInclude;
            existing.ReviewOwner = document.ReviewOwner;
            existing.Synonyms = document.Synonyms;
            existing.AllowedIntents = document.AllowedIntents;
            existing.FollowUps = document.FollowUps;
            existing.UpdatedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        AiKnowledgeDocument? existing = await _db.Set<AiKnowledgeDocument>()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (existing is not null)
        {
            _db.Set<AiKnowledgeDocument>().Remove(existing);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static KnowledgeDocument MapToDocument(AiKnowledgeDocument entity) => new(
        entity.Id, entity.Title, entity.Section, entity.Domain, entity.Status,
        entity.Version, entity.EffectiveDate, entity.Content, entity.Url,
        entity.Synonyms, entity.AlwaysInclude, entity.ReviewOwner,
        entity.AllowedIntents, entity.FollowUps);
}
