using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiTurnRouter(
    MoeDbContext db,
    ICurrentUser currentUser,
    SensitiveDataRedactor redactor,
    AiTurnPlannerService turnPlanner,
    FallbackHandler fallbackHandler,
    PaymentQueryHandler paymentHandler,
    KnowledgeAnswerHandler knowledgeHandler,
    FasInterviewHandler fasHandler,
    ILogger<AiTurnRouter> logger,
    IConfiguration configuration,
    AiAgenticTurnService? agenticService = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedDomains = ["FAS", "PAYMENT", "GENERAL"];
    private static readonly string[] AllowedRoutePrefixes =
    [
        "/portal/account", "/portal/bills", "/portal/courses", "/portal/dashboard",
        "/portal/education-account", "/portal/fas", "/portal/profile"
    ];
    private static readonly HashSet<string> AllowedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "isWelfareHomeResident", "monthlyHouseholdIncome", "householdMemberCount",
        "parentNationalities", "employmentStatusCode", "otherMonthlyIncome", "email"
    };

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        AiChatRequest sanitized = SanitizeRequest(request);
        AiConversation conversation = await GetOrCreateConversation(sanitized.ConversationId, personId, now, ct);
        string pageJson = sanitized.PageContext is null ? null! : JsonSerializer.Serialize(sanitized.PageContext, JsonOptions);
        db.Add(AiMessage.Create(conversation.Id, "USER", redactor.Redact(sanitized.Message), now));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            AiTurnPlan plan = await turnPlanner.PlanAsync(sanitized, conversation, ct);

            // Agentic path — model selects tools via FunctionChoiceBehavior.Auto()
            // Skip for active FAS sessions — deterministic handler owns the structured flow
            bool hasActiveFasSession = conversation.FasSession?.StatusCode is not null;
            if (agenticService is not null && configuration.GetValue("AiCopilot:AgenticEnabled", true) && !hasActiveFasSession)
            {
                try
                {
                    AiHandlerResult agenticResult = await agenticService.ExecuteTurnAsync(conversation, sanitized, ct);
                    if (agenticResult is not null)
                        return await SaveAndReturn(conversation.Id, pageJson, now, 0, agenticResult, conversation, sanitized, plan, stopwatch, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Agentic path failed for conversation {ConversationId}, falling back to deterministic", conversation.Id);
                }
            }

            // Clarify FAS typo
            if (plan.Intent == AiPlannerIntent.ClarifyFasTypo)
            {
                var clarification = new AiHandlerResult(
                    "Did you mean FAS, Financial Assistance Schemes? I can help you check eligibility and prepare the application form.",
                    "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")])
                {
                    FollowUpQuestions = ["Yes, help me check FAS eligibility.", "What is FAS?", "What documents do I need for FAS?"],
                    TurnIntent = "CLARIFY_FAS_TYPO",
                    ConversationPhase = plan.Phase
                };
                conversation.Touch("GENERAL", pageJson, now);
                return await SaveAssistant(conversation.Id, clarification, plan, sanitized, stopwatch, ct);
            }

            plan = AiKeywordMatchers.NormalizePlannerIntentForCompositeTurn(plan, sanitized.Message);
            string mode = AiKeywordMatchers.ModeFromPlan(plan) ?? AiKeywordMatchers.DetermineMode(sanitized.Message, conversation.ModeCode, sanitized.PageContext?.Domain);
            FasInterviewHandler.ApplyFasTaskInterruptBeforeNonFasTurn(conversation, sanitized.Message, mode);

            AiHandlerResult handlerResult = mode switch
            {
                "PAYMENT" => await paymentHandler.HandlePaymentAsync(sanitized, ct),
                "FAS_INTERVIEW" => await fasHandler.HandleFasAsync(conversation, sanitized, now, ct),
                _ => await knowledgeHandler.HandleGeneralAsync(conversation, sanitized, ct)
            };

            return await SaveAndReturn(conversation.Id, pageJson, now, 0, handlerResult, conversation, sanitized, plan, stopwatch, ct);
        }
        catch (ConcurrencyConflictException)
        {
            throw; // propagate to controller for HTTP 409
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Guid reviewId = await fallbackHandler.CreateReviewAsync(conversation, conversation.PersonId, "MODEL_OR_TOOL_FAILURE",
                sanitized.PageContext, sanitized.Message, now, ct);
            logger.LogError(ex, "AI conversation {ConversationId} failed after {ElapsedMs} ms", conversation.Id, stopwatch.ElapsedMilliseconds);
            AiHandlerResult fbResult = conversation.FasSession?.StatusCode switch
            {
                "CANCELLED" => new AiHandlerResult(
                    "Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.",
                    "GENERAL", new(false, []), [], []),
                "PAUSED" => new AiHandlerResult(
                    AiKeywordMatchers.LooksLikeScopeTest(sanitized.Message)
                        ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                        : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.",
                    "GENERAL", new(false, []), [], []),
                _ => fallbackHandler.FallbackResponse(reviewId)
            };
            AiChatResponse fbResponse = FasInterviewHandler.AttachDormantFasState(
                ToChatResponse(conversation.Id, 0, fbResult), conversation.FasSession);
            var fbMessage = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(fbResponse.Text), now,
                latencyMs: (int)stopwatch.ElapsedMilliseconds,
                responseJson: redactor.Redact(AiResponseBuilder.SerializeResponse(fbResponse)));
            db.Add(fbMessage); await db.SaveChangesAsync(ct);
            return fbResponse with { MessageId = fbMessage.Id };
        }
    }

    public async Task<AiConversationResponse> GetConversationAsync(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiConversation conversation = await db.Set<AiConversation>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        var messageRows = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.RoleCode, x.Content, x.CreatedAtUtc, x.ResponseJson })
            .ToArrayAsync(ct);
        var messages = messageRows.Select(x => new AiConversationMessageResponse(
            x.Id, x.RoleCode, x.Content, x.CreatedAtUtc,
            x.ResponseJson == null ? null : JsonSerializer.Deserialize<object>(x.ResponseJson, JsonOptions))).ToArray();
        return new(conversation.Id, conversation.ModeCode, conversation.StatusCode, messages,
            DeserializeInterviewState(conversation.FasSession?.CollectedFactsJson));
    }

    public async Task<object> CreateCaseAsync(CreateAdminCenterCaseRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiReviewRecord review = await db.Set<AiReviewRecord>()
            .SingleOrDefaultAsync(x => x.Id == request.ReviewRecordId && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.REVIEW_NOT_FOUND");
        var item = AdminCenterCase.Create(review.Id, personId, redactor.Redact(request.Description),
            request.ContactPreference, DateTime.UtcNow);
        db.Add(item); await db.SaveChangesAsync(ct);
        return new { caseId = item.Id, status = item.StatusCode, createdAtUtc = item.CreatedAtUtc };
    }

    private async Task<AiChatResponse> SaveAndReturn(Guid conversationId, string pageJson, DateTime now, long messageId,
        AiHandlerResult result, AiConversation conversation, AiChatRequest request, AiTurnPlan plan, Stopwatch stopwatch, CancellationToken ct)
    {
        AiChatResponse response = ToChatResponse(conversationId, messageId, result);
        response = FasInterviewHandler.AttachDormantFasState(response, conversation.FasSession);
        response = AiResponseBuilder.AttachV2Metadata(response, plan);
        response = AiResponseBuilder.AttachFollowUps(response, request);
        conversation.Touch(response.Mode, pageJson, now);
        var assistant = AiMessage.Create(conversationId, "ASSISTANT", redactor.Redact(response.Text), DateTime.UtcNow,
            JsonSerializer.Serialize(response.Grounding.Citations, JsonOptions),
            JsonSerializer.Serialize(response.Cards.Select(x => x.Type), JsonOptions),
            (int)stopwatch.ElapsedMilliseconds,
            redactor.Redact(AiResponseBuilder.SerializeResponse(response)));
        db.Add(assistant);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConcurrencyConflictException("FAS session modified by concurrent request", ex); }
        logger.LogInformation("AI conversation {ConversationId} mode {Mode} completed in {ElapsedMs} ms",
            conversationId, response.Mode, stopwatch.ElapsedMilliseconds);
        return response with { MessageId = assistant.Id };
    }

    private async Task<AiChatResponse> SaveAssistant(Guid conversationId, AiHandlerResult result, AiTurnPlan plan, AiChatRequest request, Stopwatch stopwatch, CancellationToken ct)
    {
        AiChatResponse response = ToChatResponse(conversationId, 0, result);
        response = AiResponseBuilder.AttachV2Metadata(response, plan);
        response = AiResponseBuilder.AttachFollowUps(response, request);
        var assistant = AiMessage.Create(conversationId, "ASSISTANT", redactor.Redact(response.Text), DateTime.UtcNow,
            JsonSerializer.Serialize(response.Grounding.Citations, JsonOptions),
            JsonSerializer.Serialize(response.Cards.Select(x => x.Type), JsonOptions),
            (int)stopwatch.ElapsedMilliseconds,
            redactor.Redact(AiResponseBuilder.SerializeResponse(response)));
        db.Add(assistant);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConcurrencyConflictException("FAS session modified by concurrent request", ex); }
        logger.LogInformation("AI conversation {ConversationId} mode {Mode} completed in {ElapsedMs} ms",
            conversationId, response.Mode, stopwatch.ElapsedMilliseconds);
        return response with { MessageId = assistant.Id };
    }

    private async Task<AiConversation> GetOrCreateConversation(Guid? id, long personId, DateTime now, CancellationToken ct)
    {
        if (id.HasValue)
        {
            var existing = await db.Set<AiConversation>().Include(x => x.FasSession)
                .SingleOrDefaultAsync(x => x.Id == id.Value, ct);
            if (existing is not null && existing.PersonId != personId)
                throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");
            if (existing is not null) return existing;
        }
        var created = AiConversation.Start(id ?? Guid.NewGuid(), personId, now);
        db.Add(created); return created;
    }

    private static AiChatResponse ToChatResponse(Guid conversationId, long messageId, AiHandlerResult r) => new(
        conversationId, messageId, r.Text, r.Mode, r.Grounding, r.Cards, r.Actions, r.InterviewState, r.ReviewRecordId)
    {
        FollowUpQuestions = r.FollowUpQuestions,
        TurnIntent = r.TurnIntent,
        ConversationPhase = r.ConversationPhase
    };

    private static AiInterviewState? DeserializeInterviewState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var state = JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions);
        return state is null ? null : new AiInterviewState(state.Status, null, [], [], null);
    }

    private static AiChatRequest SanitizeRequest(AiChatRequest request) => new()
    {
        ConversationId = request.ConversationId,
        Message = request.Message.Trim(),
        PageContext = SanitizePageContext(request.PageContext)
    };

    public static AiPageContext? SanitizePageContext(AiPageContext? pageContext)
    {
        if (pageContext is null) return null;
        string domain = AllowedDomains.Contains(pageContext.Domain?.ToUpperInvariant())
            ? pageContext.Domain!.ToUpperInvariant() : "GENERAL";
        string? path = IsAllowedPath(pageContext.Path) ? pageContext.Path : null;
        string? surface = string.IsNullOrWhiteSpace(pageContext.Surface) ? null
            : pageContext.Surface.Length > 80 ? pageContext.Surface[..80] : pageContext.Surface;
        JsonElement? entity = null;
        if (pageContext.Entity.HasValue && domain == "FAS")
        {
            var e = pageContext.Entity.Value;
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("fieldKey", out JsonElement fk) &&
                fk.ValueKind == JsonValueKind.String && fk.GetString() is string fkStr && AllowedFieldKeys.Contains(fkStr))
                entity = e;
        }
        return new AiPageContext(domain, surface, path, entity);
    }

    private static bool IsAllowedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) return false;
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal)) return false;
        return AllowedRoutePrefixes.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith($"{p}/", StringComparison.OrdinalIgnoreCase));
    }

    // ── Sanitization (stays in router) ──────────────────────────────────
}
