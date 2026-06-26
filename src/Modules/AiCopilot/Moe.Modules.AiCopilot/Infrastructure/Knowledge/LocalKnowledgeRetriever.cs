using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly KnowledgeDocument[] Documents =
    [
        new("PAY-ACCOUNT-001", "Student Finance AI Scope", "Education account and outstanding charges", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "The assistant presents current Education Account balance, total outstanding charges, and net available amount. Outstanding charges are highlighted before new enrolments or withdrawals.", "/portal/account"),
        new("PAY-METHOD-001", "Student Finance AI Scope", "Payment methods", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Course fees and bills may use Education Account funds, online payment, or split payment when supported. Insufficient Education Account funds require another payment source for the remainder.", "/portal/bills"),
        new("PAY-REFUND-001", "Student Finance AI Scope", "Refund guidance", "PAYMENT", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Refund explanations must use the enrollment's snapshotted refund policy and actual refund status. The assistant must not invent eligibility, amounts, timelines, or documentation.", "/portal/courses"),
        new("FAS-ELIG-001", "Student Finance AI Scope", "FAS eligibility interview", "FAS", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "The FAS interview collects only criteria not already known from the authenticated profile. PCI equals monthly household income divided by household member count. Eligibility and tier matching are evaluated by application code.", "/portal/fas"),
        new("FAS-PREFILL-001", "Student Finance AI Scope", "FAS form prefill", "FAS", "OFFICIAL", "3.0", new DateOnly(2026, 6, 24),
            "Singpass and profile fields are prefilled and read-only. Confirmed conversational answers are editable. Unmapped fields remain blank and require manual completion. AI is optional and the form must remain usable when AI fails.", "/portal/fas"),
        new("PROTO-WITHDRAW-001", "Prototype Finance Guidance", "Education Account withdrawal", "PAYMENT", "PROTOTYPE", "1.0", new DateOnly(2026, 6, 24),
            "Withdrawal policy is not represented by a live transactional tool in this prototype. Direct users to the Education Account page and Admin Center for authoritative eligibility, limits, and timelines.", "/portal/account")
    ];

    private static readonly Dictionary<string, double> StatusRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OFFICIAL"] = 3.0, ["GUIDE"] = 2.0, ["FAQ"] = 1.0, ["PROTOTYPE"] = 0.0
    };

    public IReadOnlyList<KnowledgeResult> Retrieve(string query, string? domain, int limit = 4)
    {
        HashSet<string> terms = Tokenize(query);
        string normalizedDomain = domain?.ToUpperInvariant() ?? "GENERAL";
        return Documents.Select(doc =>
            {
                HashSet<string> documentTerms = Tokenize($"{doc.Title} {doc.Section} {doc.Content}");
                double lexical = terms.Count == 0 ? 0 : terms.Count(documentTerms.Contains) / (double)terms.Count;
                double phrase = doc.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1.5 : 0;
                double domainBoost = doc.Domain == normalizedDomain ? 1.25 : 0;
                double rankWeight = StatusRank.GetValueOrDefault(doc.Status, 0);
                return new KnowledgeResult(new KnowledgeCitation(doc.Id, doc.Title, doc.Section, doc.Status,
                    doc.Version, doc.EffectiveDate, doc.Url), doc.Content, lexical + phrase + domainBoost + rankWeight);
            })
            .Where(x => x.Score > 0.25)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Citation.SourceId, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 8))
            .ToArray();
    }

    private static HashSet<string> Tokenize(string value) => Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
        .Select(match => match.Value).Where(term => term.Length > 2).ToHashSet(StringComparer.Ordinal);

    private sealed record KnowledgeDocument(string Id, string Title, string Section, string Domain, string Status,
        string Version, DateOnly EffectiveDate, string Content, string? Url);
}
