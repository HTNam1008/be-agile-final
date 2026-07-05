using Microsoft.SemanticKernel.Embeddings;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IKnowledgeDocumentStore _store;
    private readonly ITextEmbeddingGenerationService _embeddings;
    private KnowledgeDocument[]? _documents;
    private ReadOnlyMemory<float>[]? _documentEmbeddings;
    private DateTime _lastLoadUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public LocalKnowledgeRetriever(IKnowledgeDocumentStore store, ITextEmbeddingGenerationService embeddings)
    {
        _store = store;
        _embeddings = embeddings;
    }

    private async Task EnsureDocumentsLoadedAsync(CancellationToken ct)
    {
        if (_documents is not null && DateTime.UtcNow - _lastLoadUtc < CacheDuration) return;
        _documents = (await _store.GetAllAsync(ct)).ToArray();
        _documentEmbeddings = null;
        _lastLoadUtc = DateTime.UtcNow;
    }

    public async Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(string query, string? domain, int limit = 4, CancellationToken ct = default)
    {
        await EnsureDocumentsLoadedAsync(ct);
        ReadOnlyMemory<float>[] docEmbeddings = await GetDocumentEmbeddingsAsync(ct);
        return await SemanticRetrieveAsync(query, domain, docEmbeddings, limit, ct);
    }

    private async Task<ReadOnlyMemory<float>[]> GetDocumentEmbeddingsAsync(CancellationToken ct)
    {
        if (_documentEmbeddings is not null) return _documentEmbeddings;

        string[] texts = _documents!.Select(d => $"{d.Title}\n{d.Section}\n{d.Content}\n{string.Join(" ", d.Synonyms ?? [])}").ToArray();
        IList<ReadOnlyMemory<float>> embeddings = await _embeddings.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
        _documentEmbeddings = embeddings.ToArray();
        return _documentEmbeddings;
    }

    private async Task<IReadOnlyList<KnowledgeResult>> SemanticRetrieveAsync(string query, string? domain, ReadOnlyMemory<float>[] docEmbeddings, int limit, CancellationToken ct)
    {
        ReadOnlyMemory<float> queryEmbedding = await _embeddings.GenerateEmbeddingAsync(query, cancellationToken: ct);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";

        var scored = _documents!
            .Select((doc, i) => (Doc: doc, Score: (double)CosineSimilarity(queryEmbedding.Span, docEmbeddings[i].Span)))
            .Where(x => x.Score > 0.72)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .ToList();

        scored = DeduplicateConflicting(scored);

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 8))
            .Select(MakeResult)
            .ToArray();
    }

    private static KnowledgeResult MakeResult((KnowledgeDocument Doc, double Score) x) => new(
        new KnowledgeCitation(x.Doc.Id, x.Doc.Title, x.Doc.Section, x.Doc.Status,
            x.Doc.Version, x.Doc.EffectiveDate, x.Doc.Url),
        x.Doc.Content, x.Score,
        x.Doc.FollowUps ?? DefaultFollowUps(x.Doc.Domain, x.Doc.Title),
        x.Doc.AllowedIntents ?? DefaultAllowedIntents(x.Doc.Domain),
        x.Doc.ReviewOwner);

    private static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private static List<(KnowledgeDocument Doc, double Score)> DeduplicateConflicting(List<(KnowledgeDocument Doc, double Score)> scored)
    {
        var deduped = new List<(KnowledgeDocument Doc, double Score)>();
        foreach (var item in scored)
        {
            var existing = deduped.FirstOrDefault(d =>
                d.Doc.Domain == item.Doc.Domain &&
                d.Doc.EffectiveDate != item.Doc.EffectiveDate &&
                ConflictingContent(d.Doc, item.Doc));
            if (existing.Doc is null)
            {
                deduped.Add(item);
            }
            else if (item.Doc.EffectiveDate > existing.Doc.EffectiveDate)
            {
                deduped.Remove(existing);
                deduped.Add(item);
            }
        }
        return deduped;
    }

    private static bool ConflictingContent(KnowledgeDocument a, KnowledgeDocument b)
    {
        string[] overlapKeywords = ["bursary", "hecb", "heb", "subsidy"];
        string aText = (a.Title + " " + a.Section + " " + string.Join(" ", a.Synonyms ?? [])).ToLowerInvariant();
        string bText = (b.Title + " " + b.Section + " " + string.Join(" ", b.Synonyms ?? [])).ToLowerInvariant();
        return overlapKeywords.Any(k => aText.Contains(k) && bText.Contains(k));
    }

    private static string[] DefaultAllowedIntents(string domain) =>
        domain.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase)
            ? ["AnswerKnowledgeQuestion", "PaymentQuery"]
            : ["AnswerKnowledgeQuestion", "StartInterview", "ContinueInterview"];

    private static string[] DefaultFollowUps(string domain, string title)
    {
        if (domain.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase))
        {
            return ["Show my outstanding bills.", "How can I pay a bill?", "Explain refunds."];
        }
        if (title.Contains("Application", StringComparison.OrdinalIgnoreCase))
        {
            return ["What documents prove income?", "Which schemes can I apply for?", "How is PCI calculated?"];
        }
        if (title.Contains("Bursary", StringComparison.OrdinalIgnoreCase) || title.Contains("Subsidy", StringComparison.OrdinalIgnoreCase))
        {
            return ["Which schemes can I apply for?", "What documents prove income?", "How is PCI calculated?"];
        }
        return ["How is PCI calculated?", "Which schemes can I apply for?", "What documents prove income?"];
    }
}
