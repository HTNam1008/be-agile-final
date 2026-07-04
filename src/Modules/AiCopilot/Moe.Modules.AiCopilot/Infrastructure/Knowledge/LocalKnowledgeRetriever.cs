using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly Dictionary<string, double> StatusRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OFFICIAL"] = 3.0,
        ["GUIDE"] = 2.0,
        ["FAQ"] = 1.0,
        ["PROTOTYPE"] = 0.0
    };

    private readonly IKnowledgeDocumentStore _store;
    private readonly ITextEmbeddingGenerationService? _embeddings;
    private KnowledgeDocument[]? _documents;
    private ReadOnlyMemory<float>[]? _documentEmbeddings;
    private bool _initAttempted;

    public LocalKnowledgeRetriever(IServiceProvider services, IKnowledgeDocumentStore store)
    {
        _store = store;
        ITextEmbeddingGenerationService? embeddings = null;
        try
        {
            Kernel kernel = services.GetRequiredService<Kernel>();
            embeddings = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        }
        catch
        {
        }
        _embeddings = embeddings;
    }

    private async Task EnsureDocumentsLoadedAsync(CancellationToken ct)
    {
        if (_initAttempted) return;
        _initAttempted = true;
        _documents = (await _store.GetAllAsync(ct)).ToArray();
    }

    public async Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(string query, string? domain, int limit = 4, CancellationToken ct = default)
    {
        await EnsureDocumentsLoadedAsync(ct);

        ReadOnlyMemory<float>[]? docEmbeddings = await GetDocumentEmbeddingsAsync(ct);
        if (docEmbeddings is not null && _embeddings is not null)
        {
            return await SemanticRetrieveAsync(query, domain, docEmbeddings, limit, ct);
        }

        return LegacyRetrieve(query, domain, limit);
    }

    private async Task<ReadOnlyMemory<float>[]?> GetDocumentEmbeddingsAsync(CancellationToken ct)
    {
        if (_documentEmbeddings is not null) return _documentEmbeddings;
        if (_embeddings is null || _documents is null) return null;

        try
        {
            string[] texts = _documents.Select(d => $"{d.Title}\n{d.Section}\n{d.Content}\n{string.Join(" ", d.Synonyms ?? [])}").ToArray();
            IList<ReadOnlyMemory<float>> embeddings = await _embeddings.GenerateEmbeddingsAsync(texts, cancellationToken: ct);
            _documentEmbeddings = embeddings.ToArray();
            return _documentEmbeddings;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<KnowledgeResult>> SemanticRetrieveAsync(string query, string? domain, ReadOnlyMemory<float>[] docEmbeddings, int limit, CancellationToken ct)
    {
        ReadOnlyMemory<float> queryEmbedding = await _embeddings!.GenerateEmbeddingAsync(query, cancellationToken: ct);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";

        var scored = _documents!
            .Select((doc, i) => (Doc: doc, Score: (double)CosineSimilarity(queryEmbedding.Span, docEmbeddings[i].Span)))
            .Where(x => x.Score > 0.72)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .ToList();

        HashSet<string> includedIds = [.. scored.Select(x => x.Doc.Id)];
        foreach (var doc in _documents!.Where(d => d.AlwaysInclude && d.Domain == normalizedDomain && !includedIds.Contains(d.Id)))
        {
            scored.Add((doc, 0.0));
        }

        scored = DeduplicateConflicting(scored);

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 8))
            .Select(MakeResult)
            .ToArray();
    }

    private IReadOnlyList<KnowledgeResult> LegacyRetrieve(string query, string? domain, int limit)
    {
        HashSet<string> terms = ExpandQueryTerms(query);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";

        var scored = _documents!.Select(doc =>
        {
            HashSet<string> documentTerms = Tokenize($"{doc.Title} {doc.Section} {doc.Content}");
            foreach (string synonym in doc.Synonyms ?? [])
            {
                foreach (string term in Tokenize(synonym))
                    documentTerms.Add(term);
            }

            double lexical = terms.Count == 0 ? 0 : terms.Count(documentTerms.Contains) / (double)terms.Count;
            double phrase = doc.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1.5 : 0;
            double domainBoost = doc.Domain == normalizedDomain ? 1.25 : 0;
            double synonymBoost = (doc.Synonyms ?? []).Any(s => query.Contains(s, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0;
            double schemeBoost = SchemeSpecificBoost(doc, terms);
            double documentBoost = DocumentSpecificBoost(doc, terms);
            double rankWeight = StatusRank.GetValueOrDefault(doc.Status, 0);

            return (Doc: doc, Score: lexical + phrase + domainBoost + synonymBoost + schemeBoost + documentBoost + rankWeight);
        })
        .Where(x => x.Score > 0.25)
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
        .ToList();

        HashSet<string> includedIds = [.. scored.Select(x => x.Doc.Id)];
        foreach (var doc in _documents.Where(d => d.AlwaysInclude && d.Domain == normalizedDomain && !includedIds.Contains(d.Id)))
        {
            scored.Add((doc, StatusRank.GetValueOrDefault(doc.Status, 0)));
        }

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

    private static HashSet<string> Tokenize(string value) => Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
        .Select(match => match.Value).Where(term => term.Length > 2).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ExpandQueryTerms(string query)
    {
        HashSet<string> terms = Tokenize(query);
        string normalized = query.ToLowerInvariant();
        AddConceptTerms(terms, normalized,
            @"\b(can'?t|cannot|unable|struggl\w*|hard|difficult|not enough|low income|doesn'?t earn much|dont earn much|do not earn much|less income|poor)\b",
            ["financial", "assistance", "fas", "aid", "income", "subsidy", "bursary"]);
        AddConceptTerms(terms, normalized,
            @"\b(school fees?|course fees?|education costs?|school costs?|fees?|pay for school|pay school)\b",
            ["fees", "school", "course", "subsidy", "bursary", "financial", "assistance", "fas"]);
        AddConceptTerms(terms, normalized,
            @"\b(help|support|relief|assistance|aid)\b",
            ["financial", "assistance", "fas", "aid"]);
        return terms;
    }

    private static void AddConceptTerms(HashSet<string> terms, string normalizedQuery, string pattern, string[] concepts)
    {
        if (!Regex.IsMatch(normalizedQuery, pattern, RegexOptions.IgnoreCase))
            return;
        foreach (string concept in concepts)
            terms.Add(concept);
    }

    private static double SchemeSpecificBoost(KnowledgeDocument doc, HashSet<string> queryTerms)
    {
        string schemeText = $"{doc.Id} {doc.Title} {doc.Section} {string.Join(' ', doc.Synonyms ?? [])}";
        HashSet<string> schemeTerms = Tokenize(schemeText);
        string[] schemeKeywords =
        [
            "bursary", "hecb", "heb", "tiered", "subsidy", "isb", "jc", "ci",
            "apply", "application", "process", "steps", "portal", "autofill", "documents"
        ];

        return queryTerms
            .Where(term => schemeKeywords.Contains(term, StringComparer.OrdinalIgnoreCase) && schemeTerms.Contains(term))
            .Sum(_ => 1.75);
    }

    private static double DocumentSpecificBoost(KnowledgeDocument doc, HashSet<string> queryTerms)
    {
        if (!queryTerms.Overlaps(new[] { "document", "documents", "proof", "income", "submitting", "submit" }))
            return 0;
        string text = $"{doc.Title} {doc.Section} {doc.Content} {string.Join(' ', doc.Synonyms ?? [])}";
        if (!Regex.IsMatch(text, @"\b(supporting documents|income proof|payslips?|cpf|iras|attach documents)\b", RegexOptions.IgnoreCase))
            return 0;
        return 3.5;
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
