using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiOrchestratorService(
    MoeDbContext db,
    ICurrentUser currentUser,
    SensitiveDataRedactor redactor,
    AiTurnPlannerService turnPlanner,
    FallbackHandler fallbackHandler,
    PaymentQueryHandler paymentHandler,
    KnowledgeAnswerHandler knowledgeHandler,
    FasInterviewHandler fasHandler,
    ILogger<AiOrchestratorService> logger,
    IConfiguration configuration,
    AiAgenticTurnService? agenticService = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        AiChatRequest sanitizedRequest = new()
        {
            ConversationId = request.ConversationId,
            Message = request.Message.Trim(),
            PageContext = AiTurnRouter.SanitizePageContext(request.PageContext)
        };
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        AiConversation conversation = await GetOrCreateConversation(sanitizedRequest.ConversationId, personId, now, ct);
        string pageJson = sanitizedRequest.PageContext is null ? null! : JsonSerializer.Serialize(sanitizedRequest.PageContext, JsonOptions);
        db.Add(AiMessage.Create(conversation.Id, "USER", redactor.Redact(sanitizedRequest.Message), now));
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            AiTurnPlan turnPlan = await turnPlanner.PlanAsync(sanitizedRequest, conversation, ct);

            // Agentic path: when enabled, try the model-driven path first for GENERAL mode
            // Falls back to deterministic handlers on exception or null result.
            string preMode = AiTurnRouter.ModeFromPlan(turnPlan) ?? AiTurnRouter.DetermineMode(sanitizedRequest.Message, conversation.ModeCode, sanitizedRequest.PageContext?.Domain);
            if (agenticService is not null && configuration.GetValue("AiCopilot:AgenticEnabled", true) && preMode == "GENERAL")
            {
                try
                {
                    AiHandlerResult agenticResult = await agenticService.ExecuteTurnAsync(conversation, sanitizedRequest, ct);
                    if (agenticResult is not null)
                    {
                        AiChatResponse agenticResponse = ToChatResponse(conversation.Id, 0, agenticResult);
                        agenticResponse = FasInterviewHandler.AttachDormantFasState(agenticResponse, conversation.FasSession);
                        agenticResponse = AiResponseBuilder.AttachV2Metadata(agenticResponse, turnPlan);
                        agenticResponse = AiResponseBuilder.AttachFollowUps(agenticResponse, sanitizedRequest);
                        conversation.Touch(agenticResponse.Mode, pageJson, now);
                        var agenticMessage = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(agenticResponse.Text), now,
                            JsonSerializer.Serialize(agenticResponse.Grounding.Citations, JsonOptions),
                            JsonSerializer.Serialize(agenticResponse.Cards.Select(x => x.Type), JsonOptions),
                            (int)stopwatch.ElapsedMilliseconds, redactor.Redact(AiResponseBuilder.SerializeResponse(agenticResponse)));
                        db.Add(agenticMessage); await db.SaveChangesAsync(ct);
                        logger.LogInformation("AI conversation {ConversationId} agentic path mode {Mode} in {ElapsedMs} ms", conversation.Id, agenticResponse.Mode, stopwatch.ElapsedMilliseconds);
                        return agenticResponse with { MessageId = agenticMessage.Id };
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Agentic path failed for conversation {ConversationId}, falling back to deterministic", conversation.Id);
                }
            }

            // Handle FAS typo clarification inline (simple, no handler needed)
            if (turnPlan.Intent == AiPlannerIntent.ClarifyFasTypo)
            {
                var clarification = new AiChatResponse(
                    conversation.Id,
                    0,
                    "Did you mean FAS, Financial Assistance Schemes? I can help you check eligibility and prepare the application form.",
                    "GENERAL",
                    new(false, []),
                    [],
                    [new("NAVIGATE", "Open FAS application", "/portal/fas")],
                    null)
                {
                    FollowUpQuestions = ["Yes, help me check FAS eligibility.", "What is FAS?", "What documents do I need for FAS?"],
                    TurnIntent = "CLARIFY_FAS_TYPO",
                    ConversationPhase = turnPlan.Phase
                };
                conversation.Touch("GENERAL", pageJson, now);
                var clarificationMessage = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(clarification.Text), now,
                    latencyMs: (int)stopwatch.ElapsedMilliseconds, responseJson: redactor.Redact(AiResponseBuilder.SerializeResponse(clarification)));
                db.Add(clarificationMessage); await db.SaveChangesAsync(ct);
                return clarification with { MessageId = clarificationMessage.Id };
            }

            turnPlan = AiTurnRouter.NormalizePlannerIntentForCompositeTurn(turnPlan, sanitizedRequest.Message);
            string mode = AiTurnRouter.ModeFromPlan(turnPlan) ?? AiTurnRouter.DetermineMode(sanitizedRequest.Message, conversation.ModeCode, sanitizedRequest.PageContext?.Domain);
            FasInterviewHandler.ApplyFasTaskInterruptBeforeNonFasTurn(conversation, sanitizedRequest.Message, mode);

            AiHandlerResult handlerResult = mode switch
            {
                "PAYMENT" => await paymentHandler.HandlePaymentAsync(sanitizedRequest, ct),
                "FAS_INTERVIEW" => await fasHandler.HandleFasAsync(conversation, sanitizedRequest, now, ct),
                _ => await knowledgeHandler.HandleGeneralAsync(conversation, sanitizedRequest, ct)
            };

            AiChatResponse response = ToChatResponse(conversation.Id, 0, handlerResult);
            response = FasInterviewHandler.AttachDormantFasState(response, conversation.FasSession);
            response = AiResponseBuilder.AttachV2Metadata(response, turnPlan);
            response = AiResponseBuilder.AttachFollowUps(response, sanitizedRequest);
            conversation.Touch(response.Mode, pageJson, now);

            var assistant = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(response.Text), now,
                JsonSerializer.Serialize(response.Grounding.Citations, JsonOptions),
                JsonSerializer.Serialize(response.Cards.Select(x => x.Type), JsonOptions),
                (int)stopwatch.ElapsedMilliseconds, redactor.Redact(AiResponseBuilder.SerializeResponse(response)));
            db.Add(assistant); await db.SaveChangesAsync(ct);
            logger.LogInformation("AI conversation {ConversationId} mode {Mode} completed in {ElapsedMs} ms", conversation.Id, response.Mode, stopwatch.ElapsedMilliseconds);
            return response with { MessageId = assistant.Id };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Guid reviewId = await fallbackHandler.CreateReviewAsync(conversation, conversation.PersonId, "MODEL_OR_TOOL_FAILURE", sanitizedRequest.PageContext, sanitizedRequest.Message, now, ct);
            logger.LogError(ex, "AI conversation {ConversationId} failed after {ElapsedMs} ms", conversation.Id, stopwatch.ElapsedMilliseconds);
            AiHandlerResult fbResult = conversation.FasSession?.StatusCode switch
            {
                "CANCELLED" => new AiHandlerResult(
                    "Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.",
                    "GENERAL", new(false, []), [], []),
                "PAUSED" => new AiHandlerResult(
                    AiTurnRouter.LooksLikeScopeTest(sanitizedRequest.Message)
                        ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                        : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.",
                    "GENERAL", new(false, []), [], []),
                _ => fallbackHandler.FallbackResponse(reviewId)
            };
            AiChatResponse fbResponse = FasInterviewHandler.AttachDormantFasState(ToChatResponse(conversation.Id, 0, fbResult), conversation.FasSession);
            var fbMessage = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(fbResponse.Text), now, latencyMs: (int)stopwatch.ElapsedMilliseconds, responseJson: redactor.Redact(AiResponseBuilder.SerializeResponse(fbResponse)));
            db.Add(fbMessage); await db.SaveChangesAsync(ct);
            return fbResponse with { MessageId = fbMessage.Id };
        }
    }

    public async Task<AiConversationResponse> GetConversationAsync(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiConversation conversation = await db.Set<AiConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        var messageRows = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc, x.ResponseJson })
            .ToArrayAsync(ct);
        AiConversationMessageResponse[] messages = messageRows.Select(x => new AiConversationMessageResponse(x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc,
            x.ResponseJson == null ? null : JsonSerializer.Deserialize<object>(x.ResponseJson, JsonOptions))).ToArray();
        return new(conversation.Id, conversation.ModeCode, conversation.StatusCode, messages, DeserializeInterviewState(conversation.FasSession?.CollectedFactsJson));
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

    // ── Private helpers ──────────────────────────────────────────────────

    private async Task<AiConversation> GetOrCreateConversation(Guid? id, long personId, DateTime now, CancellationToken ct)
    {
        if (id.HasValue)
        {
            AiConversation? existing = await db.Set<AiConversation>().Include(x => x.FasSession).SingleOrDefaultAsync(x => x.Id == id.Value, ct);
            if (existing is not null && existing.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");
            if (existing is not null) return existing;
        }
        AiConversation created = AiConversation.Start(id ?? Guid.NewGuid(), personId, now); db.Add(created); return created;
    }

    private static AiChatResponse ToChatResponse(Guid conversationId, long messageId, AiHandlerResult r)
    {
        return new AiChatResponse(conversationId, messageId, r.Text, r.Mode, r.Grounding, r.Cards, r.Actions, r.InterviewState, r.ReviewRecordId)
        {
            FollowUpQuestions = r.FollowUpQuestions,
            TurnIntent = r.TurnIntent,
            ConversationPhase = r.ConversationPhase
        };
    }

    private static AiInterviewState? DeserializeInterviewState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        FasInterviewData? state = JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions);
        if (state is null) return null;
        return new AiInterviewState(state.Status, null, [], [], null);
    }
}
