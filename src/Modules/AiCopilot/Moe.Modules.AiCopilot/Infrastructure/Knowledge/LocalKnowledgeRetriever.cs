using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "as", "at", "be", "can", "do", "does", "for", "from", "how",
        "i", "in", "is", "it", "me", "my", "of", "on", "or", "the", "this", "to", "what",
        "when", "where", "which", "with", "you"
    };

    private readonly IKnowledgeDocumentStore _store;
    private readonly ILogger<LocalKnowledgeRetriever> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private KnowledgeDocument[]? _documents;
    private DateTime _lastLoadUtc = DateTime.MinValue;

    public LocalKnowledgeRetriever(IKnowledgeDocumentStore store, ILogger<LocalKnowledgeRetriever> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(string query, string? domain, int limit = 4, CancellationToken ct = default)
    {
        try
        {
            await EnsureDocumentsLoadedAsync(ct);
            string[] queryTerms = Terms(query).ToArray();
            if (queryTerms.Length == 0) return Array.Empty<KnowledgeResult>();

            var scored = _documents!
                .Where(doc => SearchesDomain(doc, domain))
                .Select(doc => (Doc: doc, Score: Score(doc, query, queryTerms)))
                .Where(x => x.Score > 0)
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Knowledge retrieval failed for query: {Query}", query);
            return Array.Empty<KnowledgeResult>();
        }
    }

    private async Task EnsureDocumentsLoadedAsync(CancellationToken ct)
    {
        if (_documents is not null && DateTime.UtcNow - _lastLoadUtc < CacheDuration) return;
        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_documents is not null && DateTime.UtcNow - _lastLoadUtc < CacheDuration) return;
            _documents = (await _store.GetAllAsync(ct)).ToArray();
            _lastLoadUtc = DateTime.UtcNow;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static bool SearchesDomain(KnowledgeDocument doc, string? domain) =>
        string.IsNullOrWhiteSpace(domain) ||
        domain.Equals("GENERAL", StringComparison.OrdinalIgnoreCase) ||
        doc.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase);

    private static double Score(KnowledgeDocument doc, string query, string[] queryTerms)
    {
        string title = doc.Title.ToUpperInvariant();
        string section = doc.Section.ToUpperInvariant();
        string content = doc.Content.ToUpperInvariant();
        string[] synonymTerms = (doc.Synonyms ?? []).SelectMany(Terms).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string fullText = $"{title} {section} {content} {string.Join(' ', synonymTerms)}";
        int hits = queryTerms.Count(term => fullText.Contains(term, StringComparison.Ordinal));
        if (hits == 0 && !doc.AlwaysInclude) return 0;

        double score = hits / (double)queryTerms.Length;
        score += queryTerms.Count(term => title.Contains(term, StringComparison.Ordinal)) * 0.30;
        score += queryTerms.Count(term => section.Contains(term, StringComparison.Ordinal)) * 0.25;
        score += queryTerms.Count(term => synonymTerms.Contains(term, StringComparer.OrdinalIgnoreCase)) * 0.35;

        string normalizedQuery = query.ToUpperInvariant();
        foreach (string synonym in doc.Synonyms ?? [])
        {
            if (normalizedQuery.Contains(synonym.ToUpperInvariant(), StringComparison.Ordinal))
                score += 0.70;
        }

        if (doc.AlwaysInclude) score += 0.10;
        if (doc.Status.Equals("OFFICIAL", StringComparison.OrdinalIgnoreCase)) score += 0.06;
        else if (doc.Status.Equals("GUIDE", StringComparison.OrdinalIgnoreCase)) score += 0.03;
        return score;
    }

    private static IEnumerable<string> Terms(string text) =>
        text.ToUpperInvariant()
            .Split([' ', '\t', '\n', '\r', ',', '.', '!', '?', ':', ';', '/', '\\', '-', '(', ')', '[', ']', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 1 && !StopWords.Contains(x));

    private static KnowledgeResult MakeResult((KnowledgeDocument Doc, double Score) x) => new(
        new KnowledgeCitation(x.Doc.Id, x.Doc.Title, x.Doc.Section, x.Doc.Status,
            x.Doc.Version, x.Doc.EffectiveDate, x.Doc.Url),
        x.Doc.Content, x.Score,
        x.Doc.FollowUps ?? DefaultFollowUps(x.Doc.Domain, x.Doc.Title),
        x.Doc.AllowedIntents ?? DefaultAllowedIntents(x.Doc.Domain),
        x.Doc.ReviewOwner);

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
