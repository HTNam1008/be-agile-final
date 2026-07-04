using System.Reflection;
using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly KnowledgeDocument[] StaticDocs =
    [
        new("PAY-ACCOUNT-001", "Student Finance AI Scope", "Education account and outstanding charges", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "The assistant presents current Education Account balance, total outstanding charges, and net available amount. Outstanding charges are highlighted before new enrolments or withdrawals.", "/portal/account", ["education account", "balance", "available amount", "outstanding charges"], false),
        new("PAY-METHOD-001", "Student Finance AI Scope", "Payment methods", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Course fees and bills may use Education Account funds, online payment, or split payment when supported. Insufficient Education Account funds require another payment source for the remainder.", "/portal/bills", ["payment method", "pay bill", "course fees", "split payment"], false),
        new("PAY-REFUND-001", "Student Finance AI Scope", "Refund guidance", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Refund explanations must use the enrollment's snapshotted refund policy and actual refund status. The assistant must not invent eligibility, amounts, timelines, or documentation.", "/portal/courses", ["refund", "withdrawal refund", "refund status", "refund policy"], false),
        new("PROTO-WITHDRAW-001", "Prototype Finance Guidance", "Education Account withdrawal", "PAYMENT", "PROTOTYPE", "1.0", new DateOnly(2026, 6, 24),
            "Withdrawal policy is not represented by a live transactional tool in this prototype. Direct users to the Education Account page and Admin Center for authoritative eligibility, limits, and timelines.", "/portal/account", ["withdraw", "withdrawal", "limits", "admin center"], false),
        new("FAS-GLOSSARY-001", "Financial Assistance Schemes", "FAS overview and terminology", "FAS", "OFFICIAL", "1.0", new DateOnly(2026, 6, 25),
            "Financial Assistance Schemes help eligible students with school fees, bursaries, and education-related costs. Eligibility commonly uses monthly gross household income (GHI), per-capita income (PCI), student level, school, and parent or guardian nationality. The FAS page remains the source of truth for live application status and available schemes.", "/portal/fas", ["financial assistance", "fas", "scheme", "aid"], false),
        new("FAS-JC-CI-001", "MOE FAS", "JC/CI eligibility and benefits", "FAS", "OFFICIAL", "1.0", new DateOnly(2026, 1, 1),
            "At Junior College or Centralised Institute level, MOE FAS eligibility can be based on monthly GHI of $4,000 or less, or monthly PCI of $1,000 or less. Benefits may include subsidy of school, miscellaneous, and examination fees, plus bursary support. Students should apply through the FAS application journey and review the final form before submission.", "/portal/fas", ["jc fas", "ci fas", "moe fas", "financial assistance scheme"], false),
        new("FAS-TIERED-SUBSIDY-001", "Tiered Fee Subsidy", "Income-tiered subsidy guidance", "FAS", "GUIDE", "1.0", new DateOnly(2026, 1, 1),
            "The Tiered Fee Subsidy uses income bands such as GHI and PCI to determine fee support. Higher support applies to lower-income tiers. Scheme names and exact subsidy values may vary by level and school, so students should use the FAS page to check live eligible schemes and submit only after reviewing the form.", "/portal/fas", ["tiered fee subsidy", "income tier", "subsidy", "isb"], false),
        new("FAS-BURSARY-FULLTIME-001", "Government Bursary", "Full-time higher education bursary guidance", "FAS", "OFFICIAL", "1.0", new DateOnly(2026, 8, 1),
            "Government bursaries for full-time ITE, polytechnic, arts institution, and autonomous university students use income tiers such as GHI and PCI. Examples include Higher Education Community Bursary and Higher Education Bursary. Amounts vary by course type and tier; students should confirm live eligibility and selected schemes in the FAS application.", "/portal/fas", ["bursary", "hecb", "heb", "government bursary", "university bursary", "polytechnic bursary"], false),
    ];

    private static readonly Dictionary<string, double> StatusRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OFFICIAL"] = 3.0,
        ["GUIDE"] = 2.0,
        ["FAQ"] = 1.0,
        ["PROTOTYPE"] = 0.0
    };

    private static readonly HashSet<string> AllowedIntents = new(StringComparer.OrdinalIgnoreCase)
    {
        "AnswerKnowledgeQuestion", "ContinueInterview", "StartInterview", "SubmitInterviewAnswer", "PaymentQuery", "Fallback"
    };

    private static readonly KnowledgeDocument[] FasChunks = LoadFasChunksFromAssembly();

    private readonly KnowledgeDocument[] _documents;

    public LocalKnowledgeRetriever()
    {
        HashSet<string> packIds = [.. FasChunks.Select(x => x.Id)];
        _documents = [.. StaticDocs.Where(x => !packIds.Contains(x.Id)), .. FasChunks];
        ValidateKnowledgePacks(_documents);
    }

    public IReadOnlyList<KnowledgeResult> Retrieve(string query, string? domain, int limit = 4)
    {
        HashSet<string> terms = Tokenize(query);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";

        var scored = _documents.Select(doc =>
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
            double rankWeight = StatusRank.GetValueOrDefault(doc.Status, 0);

            return (Doc: doc, Score: lexical + phrase + domainBoost + synonymBoost + schemeBoost + rankWeight);
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
                    x.Doc.Version, x.Doc.EffectiveDate, x.Doc.Url),
                x.Doc.Content,
                x.Score,
                x.Doc.FollowUps ?? DefaultFollowUps(x.Doc.Domain, x.Doc.Title),
                x.Doc.AllowedIntents ?? DefaultAllowedIntents(x.Doc.Domain),
                x.Doc.ReviewOwner))
            .ToArray();
    }

    private static HashSet<string> Tokenize(string value) => Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
        .Select(match => match.Value).Where(term => term.Length > 2).ToHashSet(StringComparer.Ordinal);

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
            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
            using var reader = new StreamReader(stream);
            string raw = reader.ReadToEnd();
            var (frontmatter, body) = SplitFrontmatter(raw);
            var meta = ParseFrontmatter(frontmatter);

            string domain = meta.GetValueOrDefault("domain", "FAS").ToUpperInvariant();
            string chunkId = MapChunkId(Required(meta, "chunk_id", resourceName), domain);
            string title = Required(meta, "title", resourceName);
            string status = ResolveSourceStatus(meta);
            string effectiveDateStr = meta.GetValueOrDefault("effective_date", meta.GetValueOrDefault("last_reviewed", ""));
            if (!DateOnly.TryParse(effectiveDateStr, out DateOnly effectiveDate))
            {
                effectiveDate = DateOnly.Parse(Required(meta, "last_reviewed", resourceName));
            }
            bool alwaysInclude = string.Equals(meta.GetValueOrDefault("always_include", "false"), "true", StringComparison.OrdinalIgnoreCase);
            string[] synonyms = meta.TryGetValue("synonyms", out string? synStr) ? ParseYamlList(synStr) : [];
            string[] allowedIntents = meta.TryGetValue("allowed_intents", out string? intents) ? ParseYamlList(intents) : DefaultAllowedIntents(domain);
            string[] followUps = meta.TryGetValue("follow_ups", out string? followUpText) ? ParseYamlList(followUpText) : DefaultFollowUps(domain, title);
            string content = body.Trim();
            string url = meta.GetValueOrDefault("url", domain == "PAYMENT" ? "/portal/bills" : "/portal/fas");

            chunks.Add(new KnowledgeDocument(chunkId, title, title, domain, status, "1.0", effectiveDate,
                content, url, synonyms, alwaysInclude, meta.GetValueOrDefault("review_owner", "Student Finance Product"), allowedIntents, followUps));
        }
        KnowledgeDocument[] docs = chunks.ToArray();
        ValidateKnowledgePacks(docs);
        return docs;
    }

    public static void ValidateKnowledgePacks(IEnumerable<KnowledgeDocument> docs)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KnowledgeDocument doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.Id)) throw new InvalidOperationException("Knowledge chunk is missing chunk_id.");
            if (!seen.Add(doc.Id)) throw new InvalidOperationException($"Duplicate knowledge chunk_id: {doc.Id}.");
            if (string.IsNullOrWhiteSpace(doc.Domain)) throw new InvalidOperationException($"{doc.Id} is missing domain.");
            if (string.IsNullOrWhiteSpace(doc.Title)) throw new InvalidOperationException($"{doc.Id} is missing title.");
            if (!StatusRank.ContainsKey(doc.Status)) throw new InvalidOperationException($"{doc.Id} has invalid source_status {doc.Status}.");
            if (doc.EffectiveDate > today.AddYears(2)) throw new InvalidOperationException($"{doc.Id} has an unrealistic effective_date.");
            if (string.IsNullOrWhiteSpace(doc.ReviewOwner)) throw new InvalidOperationException($"{doc.Id} is missing review_owner.");
            if (doc.Synonyms is not null && doc.Synonyms.Length == 0) throw new InvalidOperationException($"{doc.Id} has empty synonyms.");
            if (doc.AllowedIntents is not null && doc.AllowedIntents.Length == 0) throw new InvalidOperationException($"{doc.Id} has empty allowed_intents.");
            if (doc.FollowUps is not null && doc.FollowUps.Length == 0) throw new InvalidOperationException($"{doc.Id} has empty follow_ups.");
            string? invalidIntent = (doc.AllowedIntents ?? DefaultAllowedIntents(doc.Domain)).FirstOrDefault(intent => !AllowedIntents.Contains(intent));
            if (invalidIntent is not null) throw new InvalidOperationException($"{doc.Id} has invalid allowed_intent {invalidIntent}.");
        }
    }

    private static string Required(Dictionary<string, string> meta, string key, string resourceName) =>
        meta.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{resourceName} is missing required metadata '{key}'.");

    private static string ResolveSourceStatus(Dictionary<string, string> meta)
    {
        if (meta.TryGetValue("source_status", out string? status))
        {
            return status.ToUpperInvariant();
        }
        string confidence = meta.GetValueOrDefault("confidence", "medium");
        return confidence switch
        {
            "high" => "OFFICIAL",
            "medium" or "medium-verify-with-client" => "GUIDE",
            _ => "FAQ"
        };
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
            return ["Continue my FAS eligibility check.", "What documents prove income?", "Which schemes can I apply for?"];
        }
        if (title.Contains("Bursary", StringComparison.OrdinalIgnoreCase) || title.Contains("Subsidy", StringComparison.OrdinalIgnoreCase))
        {
            return ["Which schemes can I apply for?", "Continue my FAS eligibility check.", "What documents prove income?"];
        }
        return ["How is PCI calculated?", "Continue my FAS eligibility check.", "Which schemes can I apply for?"];
    }

    private static (string frontmatter, string body) SplitFrontmatter(string raw)
    {
        if (!raw.StartsWith("---", StringComparison.Ordinal))
            return ("", raw);

        int firstLineEnd = raw.IndexOf('\n');
        if (firstLineEnd < 0 || raw[..firstLineEnd].Trim() != "---")
            return ("", raw);

        int endIndex = raw.IndexOf("\n---", firstLineEnd + 1, StringComparison.Ordinal);
        if (endIndex < 0)
            return ("", raw);

        int bodyStart = raw.IndexOf('\n', endIndex + 1);
        if (bodyStart < 0)
            return (raw[(firstLineEnd + 1)..endIndex], "");

        return (raw[(firstLineEnd + 1)..endIndex], raw[(bodyStart + 1)..]);
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

    private static string MapChunkId(string chunkId, string domain)
    {
        if (domain.Equals("PAYMENT", StringComparison.OrdinalIgnoreCase))
        {
            return $"PAY-{chunkId.ToUpperInvariant()}";
        }
        return chunkId switch
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
    }

    public sealed record KnowledgeDocument(
        string Id, string Title, string Section, string Domain, string Status,
        string Version, DateOnly EffectiveDate, string Content, string? Url,
        string[]? Synonyms, bool AlwaysInclude, string ReviewOwner = "Student Finance Product",
        string[]? AllowedIntents = null, string[]? FollowUps = null);
}
