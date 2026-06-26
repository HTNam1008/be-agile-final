using System.Reflection;
using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly KnowledgeDocument[] StaticDocs =
    [
        new("PAY-ACCOUNT-001", "Student Finance AI Scope", "Education account and outstanding charges", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "The assistant presents current Education Account balance, total outstanding charges, and net available amount. Outstanding charges are highlighted before new enrolments or withdrawals.", "/portal/account", [], false),
        new("PAY-METHOD-001", "Student Finance AI Scope", "Payment methods", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Course fees and bills may use Education Account funds, online payment, or split payment when supported. Insufficient Education Account funds require another payment source for the remainder.", "/portal/bills", [], false),
        new("PAY-REFUND-001", "Student Finance AI Scope", "Refund guidance", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Refund explanations must use the enrollment's snapshotted refund policy and actual refund status. The assistant must not invent eligibility, amounts, timelines, or documentation.", "/portal/courses", [], false),
        new("PROTO-WITHDRAW-001", "Prototype Finance Guidance", "Education Account withdrawal", "PAYMENT", "PROTOTYPE", "1.0", new DateOnly(2026, 6, 24),
            "Withdrawal policy is not represented by a live transactional tool in this prototype. Direct users to the Education Account page and Admin Center for authoritative eligibility, limits, and timelines.", "/portal/account", [], false),
    ];

    private static readonly KnowledgeDocument[] FasChunks = LoadFasChunksFromAssembly();

    private static readonly Dictionary<string, double> StatusRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OFFICIAL"] = 3.0, ["GUIDE"] = 2.0, ["FAQ"] = 1.0, ["PROTOTYPE"] = 0.0
    };

    private readonly KnowledgeDocument[] _documents;

    public LocalKnowledgeRetriever()
    {
        _documents = [.. StaticDocs, .. FasChunks];
    }

    public IReadOnlyList<KnowledgeResult> Retrieve(string query, string? domain, int limit = 4)
    {
        HashSet<string> terms = Tokenize(query);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";

        var scored = _documents.Select(doc =>
        {
            HashSet<string> documentTerms = Tokenize($"{doc.Title} {doc.Section} {doc.Content}");
            foreach (string synonym in doc.Synonyms)
            {
                foreach (string term in Tokenize(synonym))
                    documentTerms.Add(term);
            }

            double lexical = terms.Count == 0 ? 0 : terms.Count(documentTerms.Contains) / (double)terms.Count;
            double phrase = doc.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1.5 : 0;
            double domainBoost = doc.Domain == normalizedDomain ? 1.25 : 0;
            double synonymBoost = doc.Synonyms.Any(s => query.Contains(s, StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0;
            double rankWeight = StatusRank.GetValueOrDefault(doc.Status, 0);

            return (Doc: doc, Score: lexical + phrase + domainBoost + synonymBoost + rankWeight);
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

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Doc.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 8))
            .Select(x => new KnowledgeResult(
                new KnowledgeCitation(x.Doc.Id, x.Doc.Title, x.Doc.Section, x.Doc.Status,
                    x.Doc.Version, x.Doc.EffectiveDate, x.Doc.Url), x.Doc.Content, x.Score))
            .ToArray();
    }

    private static HashSet<string> Tokenize(string value) => Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
        .Select(match => match.Value).Where(term => term.Length > 2).ToHashSet(StringComparer.Ordinal);

    // ── Embedded resource loader ──

    private static KnowledgeDocument[] LoadFasChunksFromAssembly()
    {
        Assembly assembly = typeof(LocalKnowledgeRetriever).Assembly;
        string[] resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.Contains("FasChunks") && n.EndsWith(".md"))
            .OrderBy(n => n)
            .ToArray();

        var chunks = new List<KnowledgeDocument>();
        foreach (string resourceName in resourceNames)
        {
            try
            {
                using Stream stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
                using var reader = new StreamReader(stream);
                string raw = reader.ReadToEnd();
                var (frontmatter, body) = SplitFrontmatter(raw);
                var meta = ParseFrontmatter(frontmatter);

                string chunkId = MapChunkId(meta.GetValueOrDefault("chunk_id", ""));
                string title = meta.GetValueOrDefault("title", resourceName);
                string confidence = meta.GetValueOrDefault("confidence", "medium");
                string status = confidence switch
                {
                    "high" => "OFFICIAL",
                    "medium" => "GUIDE",
                    _ => "FAQ"
                };
                string effectiveDateStr = meta.GetValueOrDefault("effective_date", meta.GetValueOrDefault("last_reviewed", ""));
                DateOnly effectiveDate = DateOnly.TryParse(effectiveDateStr, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
                bool alwaysInclude = string.Equals(meta.GetValueOrDefault("always_include", "false"), "true", StringComparison.OrdinalIgnoreCase);

                string[] synonyms = [];
                if (meta.TryGetValue("synonyms", out string? synStr))
                {
                    synonyms = ParseYamlList(synStr);
                }

                string content = body.Trim();

                chunks.Add(new KnowledgeDocument(chunkId, title, title, "FAS", status, "1.0", effectiveDate,
                    content, "/portal/fas", synonyms, alwaysInclude));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[KnowledgeLoader] Skipping malformed resource {resourceName}: {ex.Message}");
            }
        }
        return chunks.ToArray();
    }

    private static (string frontmatter, string body) SplitFrontmatter(string raw)
    {
        const string separator = "---\n";
        if (!raw.StartsWith(separator, StringComparison.Ordinal))
            return ("", raw);

        int endIndex = raw.IndexOf(separator, 4, StringComparison.Ordinal);
        if (endIndex < 0)
            return ("", raw);

        return (raw[4..endIndex], raw[(endIndex + 4)..]);
    }

    private static Dictionary<string, string> ParseFrontmatter(string yaml)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in yaml.Split('\n'))
        {
            var match = Regex.Match(line, @"^(\w[\w-]*):\s*(.+)$");
            if (match.Success)
            {
                result[match.Groups[1].Value] = match.Groups[2].Value.Trim().Trim('"');
            }
        }
        return result;
    }

    private static string[] ParseYamlList(string value)
    {
        value = value.Trim();
        var match = Regex.Match(value, @"^\[(.*)\]$");
        if (!match.Success)
        {
            return value.Split(',')
                .Select(x => x.Trim().Trim('"'))
                .Where(x => x.Length > 0)
                .ToArray();
        }
        return match.Groups[1].Value.Split(',')
            .Select(x => x.Trim().Trim('"'))
            .Where(x => x.Length > 0)
            .ToArray();
    }

    private static string MapChunkId(string chunkId) => chunkId switch
    {
        "chunk-01-glossary" => "FAS-GLOSSARY-001",
        "chunk-02-scope-and-fallback" => "FAS-SCOPE-001",
        "chunk-03-jc-ci-fas" => "FAS-JC-CI-001",
        "chunk-04-tiered-fee-subsidy" => "FAS-TIERED-SUBSIDY-001",
        "chunk-05-bursary-fulltime" => "FAS-BURSARY-FULLTIME-001",
        "chunk-06-bursary-parttime" => "FAS-BURSARY-PARTTIME-001",
        "chunk-07-application-process" => "FAS-APPLICATION-001",
        "chunk-08-faqs" => "FAS-FAQS-001",
        _ => $"FAS-{chunkId.ToUpperInvariant()}"
    };

    public sealed record KnowledgeDocument(
        string Id, string Title, string Section, string Domain, string Status,
        string Version, DateOnly EffectiveDate, string Content, string? Url,
        string[] Synonyms, bool AlwaysInclude);
}
