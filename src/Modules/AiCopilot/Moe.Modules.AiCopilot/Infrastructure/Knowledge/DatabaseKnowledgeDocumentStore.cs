using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class DatabaseKnowledgeDocumentStore : IKnowledgeDocumentStore
{
    private readonly MoeDbContext _db;
    private readonly Kernel _kernel;
    private readonly ILogger<DatabaseKnowledgeDocumentStore> _logger;

    private bool _seeded;

    public DatabaseKnowledgeDocumentStore(MoeDbContext db, Kernel kernel, ILogger<DatabaseKnowledgeDocumentStore> logger)
    {
        _db = db;
        _kernel = kernel;
        _logger = logger;
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
            await SetDocumentEmbeddingAsync(entity, ct);
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
            await SetDocumentEmbeddingAsync(existing, ct);
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
            await SetDocumentEmbeddingAsync(existing, ct);
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

    public async Task<IReadOnlyList<KnowledgeDocument>> SearchAsync(string query, int limit = 5, string? domain = null, CancellationToken ct = default)
    {
        await SeedIfEmptyAsync(ct);
        float[]? queryEmbedding = await GenerateEmbeddingAsync(query, ct);
        if (queryEmbedding is null)
        {
            string[] queryTerms = query.ToUpperInvariant().Split([' ', '\t', '\n', '\r', ',', '.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<AiKnowledgeDocument> all = _db.Set<AiKnowledgeDocument>().AsEnumerable();
            if (domain is not null) all = all.Where(d => d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            return all.Select(d =>
            {
                string docText = $"{d.Title} {d.Section} {d.Content}".ToUpperInvariant();
                int matchCount = queryTerms.Count(qt => docText.Contains(qt));
                return (doc: d, score: (double)matchCount / Math.Max(queryTerms.Length, 1));
            }).Where(x => x.score > 0).OrderByDescending(x => x.score).Take(limit).Select(x => MapToDocument(x.doc)).ToList();
        }

        IEnumerable<AiKnowledgeDocument> candidates = _db.Set<AiKnowledgeDocument>().AsEnumerable();
        if (domain is not null) candidates = candidates.Where(d => d.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
        return candidates
            .Select(d => (doc: d, score: CosineSimilarity(queryEmbedding, d.Embedding ?? [])))
            .Where(x => x.score > 0.5f)
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => MapToDocument(x.doc))
            .ToList();
    }

    private async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        try
        {
            ITextEmbeddingGenerationService? svc = _kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            if (svc is null) return null;
            ReadOnlyMemory<float> embedding = await svc.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return embedding.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate embedding for text (length {Length}), falling back to keyword search", text.Length);
            return null;
        }
    }

    private async Task SetDocumentEmbeddingAsync(AiKnowledgeDocument entity, CancellationToken ct)
    {
        string text = $"{entity.Title} {entity.Section} {entity.Content}";
        float[]? embedding = await GenerateEmbeddingAsync(text, ct);
        if (embedding is not null)
        {
            entity.Embedding = embedding;
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom == 0 ? 0 : (float)(dot / denom);
    }

    private static KnowledgeDocument MapToDocument(AiKnowledgeDocument entity) => new(
        entity.Id, entity.Title, entity.Section, entity.Domain, entity.Status,
        entity.Version, entity.EffectiveDate, entity.Content, entity.Url,
        entity.Synonyms, entity.AlwaysInclude, entity.ReviewOwner,
        entity.AllowedIntents, entity.FollowUps);
}
