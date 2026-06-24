using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiOrchestratorService(
    Kernel kernel, MoeDbContext db, ICurrentUser currentUser, AiFinanceReader finance,
    StudentFasApplicationService fas, IKnowledgeRetriever knowledge, SensitiveDataRedactor redactor,
    ILogger<AiOrchestratorService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedRoutes = ["/portal/account", "/portal/bills", "/portal/fas", "/portal/courses"];

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        AiConversation conversation = await GetOrCreateConversation(request.ConversationId, personId, now, ct);
        string pageJson = request.PageContext is null ? null! : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        db.Add(AiMessage.Create(conversation.Id, "USER", redactor.Redact(request.Message), now));
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            string mode = DetermineMode(request.Message, conversation.ModeCode, request.PageContext?.Domain);
            AiChatResponse response = mode switch
            {
                "PAYMENT" => await HandlePayment(conversation, request, now, ct),
                "FAS_INTERVIEW" => await HandleFas(conversation, request, now, ct),
                _ => await HandleGeneral(conversation, request, now, ct)
            };
            conversation.Touch(response.Mode, pageJson, conversation.FasInterviewJson, now);
            var assistant = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(response.Text), now,
                JsonSerializer.Serialize(response.Grounding.Citations, JsonOptions),
                JsonSerializer.Serialize(response.Cards.Select(x => x.Type), JsonOptions), (int)stopwatch.ElapsedMilliseconds);
            db.Add(assistant); await db.SaveChangesAsync(ct);
            logger.LogInformation("AI conversation {ConversationId} mode {Mode} completed in {ElapsedMs} ms", conversation.Id, response.Mode, stopwatch.ElapsedMilliseconds);
            return response with { MessageId = assistant.Id };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Guid reviewId = await CreateReview(conversation, personId, "MODEL_OR_TOOL_FAILURE", request.PageContext, request.Message, now, ct);
            logger.LogError(ex, "AI conversation {ConversationId} failed after {ElapsedMs} ms", conversation.Id, stopwatch.ElapsedMilliseconds);
            const string text = "I could not complete that request reliably. You can continue in the portal, review the help links, or contact the Admin Center.";
            var fallback = AiMessage.Create(conversation.Id, "ASSISTANT", text, now, latencyMs: (int)stopwatch.ElapsedMilliseconds);
            db.Add(fallback); await db.SaveChangesAsync(ct);
            return new AiChatResponse(conversation.Id, fallback.Id, text, "FALLBACK", new(false, []), [],
                FallbackActions(reviewId), null, reviewId);
        }
    }

    public async Task<AiConversationResponse> GetConversationAsync(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiConversation conversation = await db.Set<AiConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        AiConversationMessageResponse[] messages = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc).Select(x => new AiConversationMessageResponse(x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc)).ToArrayAsync(ct);
        return new(conversation.Id, conversation.ModeCode, conversation.StatusCode, messages, DeserializeInterview(conversation.FasInterviewJson));
    }

    public async Task<object> CreateCaseAsync(CreateAdminCenterCaseRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiReviewRecord review = await db.Set<AiReviewRecord>().SingleOrDefaultAsync(x => x.Id == request.ReviewRecordId && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.REVIEW_NOT_FOUND");
        AdminCenterCase item = AdminCenterCase.Create(review.Id, personId, redactor.Redact(request.Description), request.ContactPreference, DateTime.UtcNow);
        db.Add(item); await db.SaveChangesAsync(ct);
        return new { caseId = item.Id, status = item.StatusCode, createdAtUtc = item.CreatedAtUtc };
    }

    private async Task<AiChatResponse> HandlePayment(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        AiFinanceSnapshot snapshot = await finance.GetSnapshotAsync(ct);
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, "PAYMENT");
        string intent = request.Message.ToUpperInvariant();
        if (intent.Contains("HISTORY") || intent.Contains("PAID") || intent.Contains("REFUND"))
        {
            string historyText = snapshot.RecentPayments.Count == 0
                ? "I could not find any recent payment records for your account."
                : $"I found {snapshot.RecentPayments.Count} recent payment record(s). Refund amounts shown come from recorded payment events.";
            return new(c.Id, 0, historyText, "PAYMENT", Grounding(sources),
                [new("PAYMENT_HISTORY", snapshot.RecentPayments)],
                [new("NAVIGATE", "View bills and payments", "/portal/bills")], null);
        }
        if (intent.Contains("BILL") || intent.Contains("OUTSTANDING") || intent.Contains("DUE"))
        {
            string billText = snapshot.BillCount == 0
                ? "You have no outstanding bills."
                : $"You have {snapshot.BillCount} outstanding bill(s) totalling {snapshot.TotalOutstanding.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.";
            return new(c.Id, 0, billText, "PAYMENT", Grounding(sources),
                [new("OUTSTANDING_BILLS", snapshot.Bills)], [new("NAVIGATE", "View bills", "/portal/bills")], null);
        }
        string warning = snapshot.TotalOutstanding > 0
            ? $" You have {snapshot.TotalOutstanding.ToString("C", CultureInfo.GetCultureInfo("en-SG"))} outstanding; settle it before new enrolments or withdrawals."
            : " You have no outstanding charges.";
        string text = $"Your Education Account has {snapshot.AvailableBalance.ToString("C", CultureInfo.GetCultureInfo("en-SG"))} available. Your total outstanding charges are {snapshot.TotalOutstanding.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}, leaving a net available amount of {snapshot.NetAvailable.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.{warning}";
        AiCard card = new("FINANCE_SUMMARY", snapshot);
        AiAction[] actions = [new("NAVIGATE", "View bills", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")];
        return new(c.Id, 0, text, "PAYMENT", Grounding(sources), [card], actions, null);
    }

    private async Task<AiChatResponse> HandleFas(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        FasInterviewData state = DeserializeState(c.FasInterviewJson) ?? await InitializeFasState(ct);
        ApplyFasAnswer(state, request.Message);
        string? next = NextQuestion(state);
        object? recommendation = null;
        string text;
        if (next is null && state.IsWelfareHomeResident == false)
        {
            recommendation = await fas.CheckEligibility(new EligibilityRequest(state.MonthlyHouseholdIncome!.Value,
                state.HouseholdMemberCount!.Value, 0, state.ParentNationalities), ct);
            state.Status = "COMPLETE";
            text = "I have enough confirmed information to evaluate the active FAS schemes. Review the recommendation and apply the confirmed answers to the form when ready.";
        }
        else if (next is null)
        {
            state.Status = "COMPLETE";
            text = "Your welfare-home status is confirmed. The application form will skip income fields and continue with the required supporting information.";
        }
        else { state.Status = "COLLECTING"; text = next; }

        AiInterviewState interview = ToInterviewState(state, next);
        c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
        List<AiCard> cards = recommendation is null ? [] : [new("FAS_RECOMMENDATION", recommendation)];
        List<AiAction> actions = [new("NAVIGATE", "Open FAS application", "/portal/fas")];
        if (state.Status == "COMPLETE") actions.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: interview.FormPatch));
        return new(c.Id, 0, text, "FAS_INTERVIEW", Grounding(knowledge.Retrieve(request.Message, "FAS")), cards, actions, interview);
    }

    private async Task<AiChatResponse> HandleGeneral(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, request.PageContext?.Domain);
        var history = new ChatHistory($"""
            You are the MOE Student Finance e-Service copilot. Give concise guidance using only the supplied sources.
            Never invent personal data, policy, eligibility, amounts, status, or timelines. Label PROTOTYPE sources.
            If sources are insufficient, say so and direct the user to Admin Center. Cite source IDs in square brackets.
            Sources:
            {string.Join("\n", sources.Select(x => $"[{x.Citation.SourceId}] ({x.Citation.SourceStatus}) {x.Content}"))}
            """);
        history.AddUserMessage(request.Message);
        ChatMessageContent answer = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
        string text = string.IsNullOrWhiteSpace(answer.Content) ? "I do not have enough reliable information to answer that." : answer.Content.Trim();
        if (sources.Count == 0)
        {
            Guid review = await CreateReview(c, c.PersonId, "MISSING_POLICY", request.PageContext, request.Message, now, ct);
            return new(c.Id, 0, text, "FALLBACK", new(false, []), [], FallbackActions(review), null, review);
        }
        return new(c.Id, 0, text, "GENERAL", Grounding(sources), [], [], null);
    }

    private async Task<AiConversation> GetOrCreateConversation(Guid? id, long personId, DateTime now, CancellationToken ct)
    {
        if (id.HasValue)
        {
            AiConversation? existing = await db.Set<AiConversation>().SingleOrDefaultAsync(x => x.Id == id.Value, ct);
            if (existing is not null && existing.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");
            if (existing is not null) return existing;
        }
        AiConversation created = AiConversation.Start(id ?? Guid.NewGuid(), personId, now); db.Add(created); return created;
    }

    private async Task<Guid> CreateReview(AiConversation c, long personId, string reason, AiPageContext? page, string transcript, DateTime now, CancellationToken ct)
    {
        AiReviewRecord record = AiReviewRecord.Create(c.Id, personId, reason, page?.Domain ?? "GENERAL", page?.Path, redactor.Redact(transcript), now);
        db.Add(record); await db.SaveChangesAsync(ct); return record.Id;
    }

    private static string DetermineMode(string message, string current, string? domain)
    {
        string value = $"{domain} {message}".ToUpperInvariant();
        if (current == "FAS_INTERVIEW" || value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE") || value.Contains("ELIGIB")) return "FAS_INTERVIEW";
        if (value.Contains("PAY") || value.Contains("BILL") || value.Contains("BALANCE") || value.Contains("OUTSTANDING") || value.Contains("REFUND")) return "PAYMENT";
        return "GENERAL";
    }

    private async Task<FasInterviewData> InitializeFasState(CancellationToken ct)
    {
        JsonElement profile = JsonSerializer.SerializeToElement(await fas.Prefill(ct), JsonOptions);
        return new FasInterviewData { Profile = profile, Status = "COLLECTING" };
    }
    private static void ApplyFasAnswer(FasInterviewData s, string message)
    {
        string lower = message.ToLowerInvariant();
        if (!s.IsWelfareHomeResident.HasValue && (lower.Contains("welfare") || lower is "yes" or "no"))
        { s.IsWelfareHomeResident = lower.Contains("yes") || lower.Contains("welfare home"); return; }
        decimal[] numbers = Regex.Matches(message, @"\d+(?:\.\d+)?").Select(x => decimal.Parse(x.Value, CultureInfo.InvariantCulture)).ToArray();
        if (!s.MonthlyHouseholdIncome.HasValue && numbers.Length > 0) { s.MonthlyHouseholdIncome = numbers[0]; return; }
        if (!s.HouseholdMemberCount.HasValue && numbers.Length > 0) { s.HouseholdMemberCount = (int)numbers[0]; return; }
        if (s.ParentNationalities.Count == 0 && message.Length is > 1 and < 100) s.ParentNationalities = [message.Trim()];
    }
    private static string? NextQuestion(FasInterviewData s)
    {
        if (!s.IsWelfareHomeResident.HasValue) return "Are you currently residing in an approved welfare home?";
        if (s.IsWelfareHomeResident.Value) return null;
        if (!s.MonthlyHouseholdIncome.HasValue) return "What is your total monthly household income in SGD?";
        if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0) return "How many people are in your household?";
        if (s.ParentNationalities.Count == 0) return "What is your parent or guardian's nationality?";
        return null;
    }
    private static AiInterviewState ToInterviewState(FasInterviewData s, string? next)
    {
        List<AiInterviewField> fields =
        [
            new("isWelfareHomeResident", s.IsWelfareHomeResident, s.IsWelfareHomeResident.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.IsWelfareHomeResident.HasValue),
            new("monthlyHouseholdIncome", s.MonthlyHouseholdIncome, s.MonthlyHouseholdIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.MonthlyHouseholdIncome.HasValue),
            new("householdMemberCount", s.HouseholdMemberCount, s.HouseholdMemberCount.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.HouseholdMemberCount.HasValue),
            new("parentNationalities", s.ParentNationalities, s.ParentNationalities.Count > 0 ? "AI_CONFIRMED" : "UNMAPPED", s.ParentNationalities.Count > 0)
        ];
        string[] missing = fields.Where(x => !x.Confirmed && !(s.IsWelfareHomeResident == true && x.Name is "monthlyHouseholdIncome" or "householdMemberCount"))
            .Select(x => x.Name).ToArray();
        object patch = new { s.IsWelfareHomeResident, s.MonthlyHouseholdIncome, s.HouseholdMemberCount, s.ParentNationalities };
        return new(s.Status, next, fields, missing, patch);
    }
    private static FasInterviewData? DeserializeState(string? value) => string.IsNullOrWhiteSpace(value) ? null : JsonSerializer.Deserialize<FasInterviewData>(value, JsonOptions);
    private static AiInterviewState? DeserializeInterview(string? value) => DeserializeState(value) is { } state ? ToInterviewState(state, NextQuestion(state)) : null;
    private static AiGrounding Grounding(IReadOnlyList<KnowledgeResult> sources) => new(sources.Count > 0, sources.Select(x => x.Citation).ToArray());
    private static AiAction[] FallbackActions(Guid review) =>
    [new("NAVIGATE", "View help", "/portal/account"), new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })];

    private sealed class FasInterviewData
    {
        public string Status { get; set; } = "COLLECTING";
        public JsonElement Profile { get; set; }
        public bool? IsWelfareHomeResident { get; set; }
        public decimal? MonthlyHouseholdIncome { get; set; }
        public int? HouseholdMemberCount { get; set; }
        public List<string> ParentNationalities { get; set; } = [];
    }
}
