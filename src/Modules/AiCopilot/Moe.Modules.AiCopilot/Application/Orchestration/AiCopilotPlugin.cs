using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiCopilotPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AiFinanceReader _finance;
    private readonly IKnowledgeRetriever _knowledge;
    private readonly FasInterviewHandler _fasHandler;

    public AiCopilotPlugin(AiFinanceReader finance, IKnowledgeRetriever knowledge, FasInterviewHandler fasHandler)
    {
        _finance = finance;
        _knowledge = knowledge;
        _fasHandler = fasHandler;
    }

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
    [Description("Cancel or pause the active FAS interview session. Use when the student asks to stop, cancel, quit, or pause the FAS eligibility check.")]
    [return: Description("Confirmation message indicating whether FAS was cancelled, paused, or no active session existed")]
    public string CancelFasInterview(
        [Description("Action: 'cancel' to stop permanently, 'pause' to pause")] string action)
    {
        return action.ToLowerInvariant() switch
        {
            "cancel" => "FAS_CANCELLED",
            "pause" => "FAS_PAUSED",
            _ => "INVALID_ACTION"
        };
    }

    [KernelFunction]
    [Description("Get profile facts about the student for FAS prefill — email, nationality, institution details.")]
    [return: Description("JSON object with prefill data from the student's profile")]
    public string GetProfileFacts()
    {
        return "Facts are handled internally by the FAS interview handler.";
    }
}
