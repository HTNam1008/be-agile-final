using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiCopilotPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AiFinanceReader _finance;
    private readonly StudentFasApplicationService _fas;
    private readonly IKnowledgeRetriever _knowledge;

    public AiCopilotPlugin(AiFinanceReader finance, StudentFasApplicationService fas, IKnowledgeRetriever knowledge)
    {
        _finance = finance;
        _fas = fas;
        _knowledge = knowledge;
    }

    // Set by AiAgenticTurnService before the agentic loop runs, so CancelFasInterview
    // can mutate the tracked FasSession entity without needing MoeDbContext directly.
    internal AiConversation? CurrentConversation { get; set; }

    [KernelFunction]
    [Description("Get the student's Education Account balance, outstanding bills, recent payments, and net available amount.")]
    [return: Description("JSON object with currentBalance, heldBalance, availableBalance, totalOutstanding, netAvailable, currencyCode, billCount, nearestDueDate, bills array, recentPayments array")]
    public async Task<string> GetFinanceSnapshotAsync(CancellationToken ct)
    {
        AiFinanceSnapshot snapshot = await _finance.GetSnapshotAsync(ct);
        return JsonSerializer.Serialize(new
        {
            snapshot.CurrentBalance,
            snapshot.HeldBalance,
            snapshot.AvailableBalance,
            snapshot.TotalOutstanding,
            snapshot.NetAvailable,
            snapshot.CurrencyCode,
            snapshot.BillCount,
            nearestDueDate = snapshot.NearestDueDate?.ToString("O"),
            bills = snapshot.Bills.Select(b => new { b.BillId, b.BillNumber, b.Description, dueDate = b.DueDate.ToString("O"), b.OutstandingAmount, b.StatusCode }),
            recentPayments = snapshot.RecentPayments.Select(p => new { p.PaymentId, p.PaymentNumber, p.Amount, p.StatusCode, p.InitiatedAtUtc, p.RefundedAmount })
        }, JsonOptions);
    }

    [KernelFunction]
    [Description("Search the FAS policy knowledge base for guidance on fees, bursaries, subsidies, eligibility, applications, or documents.")]
    [return: Description("JSON array of knowledge results with title, content, source, score, and follow-up questions")]
    public async Task<string> SearchKnowledgeBaseAsync(
        [Description("Natural language search query — what the student is asking about")] string query,
        [Description("Optional domain filter: FAS, PAYMENT, or GENERAL")] string? domain,
        CancellationToken ct)
    {
        IReadOnlyList<KnowledgeResult> results = await _knowledge.RetrieveAsync(query, domain, ct: ct);
        return JsonSerializer.Serialize(results.Select(r => new
        {
            r.Citation.SourceId,
            r.Citation.Title,
            r.Citation.Section,
            r.Citation.SourceStatus,
            r.Citation.EffectiveDate,
            content = r.Content.Length > 800 ? r.Content[..800] + "..." : r.Content,
            r.Score,
            followUps = r.FollowUps,
            allowedIntents = r.AllowedIntents
        }), JsonOptions);
    }

    [KernelFunction]
    [Description("Check FAS eligibility based on income, household size, and nationality facts. Call this when the student has provided all required FAS facts and wants to see eligibility results.")]
    [return: Description("JSON object with perCapitaIncome, matchedSchemes, recommendedScheme, recommendationStatus")]
    public async Task<string> CheckFasEligibilityAsync(
        [Description("Monthly household income in SGD")] decimal monthlyHouseholdIncome,
        [Description("Number of household members")] int householdMemberCount,
        [Description("Other monthly income in SGD, defaults to 0")] decimal otherMonthlyIncome,
        [Description("Comma-separated parent nationalities, e.g. 'Singaporean,Malaysian'")] string? parentNationalities,
        CancellationToken ct)
    {
        EligibilityResponse response = await _fas.CheckEligibility(new EligibilityRequest(
            monthlyHouseholdIncome,
            householdMemberCount,
            otherMonthlyIncome,
            parentNationalities?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ), ct);
        return JsonSerializer.Serialize(new
        {
            response.PerCapitaIncome,
            response.RecommendationStatus,
            response.ManualReviewReason,
            matchedSchemes = response.MatchedSchemes.Select(s => new
            {
                s.SchemeName, s.TierLabel, s.SubsidyType, s.SubsidyValue,
                s.RecommendationRank, s.RecommendationReason, s.RecommendationConfidence,
                applicationEndDate = s.ApplicationEndDate.ToString("O"), s.IsComparable, s.CanApply
            }),
            recommendedScheme = response.RecommendedScheme is not null ? new
            {
                response.RecommendedScheme.SchemeId,
                response.RecommendedScheme.SchemeName,
                response.RecommendedScheme.Description
            } : null,
            recommendedTier = response.RecommendedTier is not null ? new
            {
                response.RecommendedTier.TierId,
                response.RecommendedTier.TierLabel,
                response.RecommendedTier.SubsidyType,
                response.RecommendedTier.SubsidyValue
            } : null
        }, JsonOptions);
    }

    [KernelFunction]
    [Description("Cancel or pause the active FAS interview session. Use when the student asks to stop, cancel, quit, or pause the FAS eligibility check.")]
    [return: Description("Confirmation message indicating whether FAS was cancelled, paused, or no active session existed")]
    public string CancelFasInterview(
        [Description("Action: 'cancel' to stop permanently, 'pause' to pause")] string action)
    {
        AiFasSession? session = CurrentConversation?.FasSession;
        if (session is null) return "NO_ACTIVE_SESSION";

        string actionL = action.ToLowerInvariant();
        if (actionL == "cancel")
        {
            session.StatusCode = "CANCELLED";
            session.UpdatedAtUtc = DateTime.UtcNow;
            return "FAS_CANCELLED";
        }
        if (actionL == "pause")
        {
            session.StatusCode = "PAUSED";
            session.UpdatedAtUtc = DateTime.UtcNow;
            return "FAS_PAUSED";
        }
        return "INVALID_ACTION";
    }

    [KernelFunction]
    [Description("Get profile facts about the student for FAS prefill — email, nationality, institution details.")]
    [return: Description("JSON object with prefill data from the student's profile")]
    public string GetProfileFacts()
    {
        return "Facts are handled internally by the FAS interview handler.";
    }
}
