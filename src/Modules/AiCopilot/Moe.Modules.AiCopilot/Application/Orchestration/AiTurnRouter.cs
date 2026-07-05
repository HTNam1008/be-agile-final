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

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        var sanitized = new AiChatRequest { ConversationId = request.ConversationId, Message = request.Message.Trim(), PageContext = AiRouterHelpers.SanitizePageContext(request.PageContext) };
        var c = await GetOrCreateConversation(sanitized.ConversationId, personId, now, ct);
        string pj = sanitized.PageContext is null ? null! : JsonSerializer.Serialize(sanitized.PageContext, JsonOptions);
        db.Add(AiMessage.Create(c.Id, "USER", redactor.Redact(sanitized.Message), now));
        var sw = Stopwatch.StartNew();

        try
        {
            // Active FAS session — route through FasInterviewHandler for interview turns;
            // let knowledge questions, capability queries, and admin center questions fall through
            // to the agentic/deterministic path so they can interrupt the FAS session.
            if (c.FasSession?.StatusCode is "COLLECTING" or "CONFIRMING" or "CLARIFYING"
                && !AiKeywordMatchers.IsFasKnowledgeInterrupt(sanitized.Message.ToUpperInvariant())
                && !AiKeywordMatchers.LooksLikeCapabilityQuestion(sanitized.Message)
                && !AiKeywordMatchers.LooksLikeAdminCenterQuestion(sanitized.Message))
            {
                var fasPlan = new AiTurnPlan(AiPlannerIntent.ContinueFas, "collecting", null, 1.0m, "ROUTER");
                var fasResult = await fasHandler.HandleAsync(c, sanitized, fasPlan, ct);
                if (fasResult.Mode is "REDIRECT_PAYMENT" or "REDIRECT_KNOWLEDGE" or "REDIRECT_FALLBACK")
                {
                    if (fasResult.Mode == "REDIRECT_FALLBACK")
                    {
                        Guid rid = await fallbackHandler.CreateReviewAsync(c, c.PersonId, fasResult.TurnIntent ?? "FAS_MANUAL_FALLBACK", sanitized.PageContext, sanitized.Message, now, ct);
                        var fb = fallbackHandler.FallbackResponse(rid);
                        fb = fb with { InterviewState = fasResult.InterviewState, FollowUpQuestions = fb.FollowUpQuestions.Count > 0 ? fb.FollowUpQuestions : fasResult.FollowUpQuestions };
                        return await Save(c.Id, pj, now, 0, fb, c, sanitized, fasPlan, sw, ct, true);
                    }
                    var dispatched = fasResult.Mode == "REDIRECT_PAYMENT"
                        ? await paymentHandler.HandleAsync(c, sanitized, fasPlan, ct)
                        : await knowledgeHandler.HandleAsync(c, sanitized, fasPlan, ct);
                    dispatched = dispatched with { InterviewState = fasResult.InterviewState };
                    return await Save(c.Id, pj, now, 0, dispatched, c, sanitized, fasPlan, sw, ct, true);
                }
                return await Save(c.Id, pj, now, 0, fasResult, c, sanitized, fasPlan, sw, ct, true);
            }

            // Agentic path — try first for all modes (FAS state machine sessions handled above)
            if (agenticService is not null && configuration.GetValue("AiCopilot:AgenticEnabled", true))
                try { var ar = await agenticService.ExecuteTurnAsync(c, sanitized, ct); if (ar is not null) return await Save(c.Id, pj, now, 0, ar, c, sanitized, new AiTurnPlan(AiPlannerIntent.Fallback, "idle", null, 0.5m, "AGENTIC"), sw, ct, true); }
                catch (Exception ex) { logger.LogWarning(ex, "Agentic path failed for conv {Id}", c.Id); }

            // Fall back to deterministic path: planner + mode dispatch
            var plan = await turnPlanner.PlanAsync(sanitized, c, ct);

            if (plan.Intent == AiPlannerIntent.ClarifyFasTypo)
            {
                c.Touch("GENERAL", pj, now);
                return await Save(c.Id, new AiHandlerResult("Did you mean FAS? I can help check eligibility.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")])
                { FollowUpQuestions = ["Yes, help me check FAS eligibility.", "What is FAS?", "What documents do I need for FAS?"], TurnIntent = "CLARIFY_FAS_TYPO", ConversationPhase = plan.Phase }, plan, sanitized, sw, ct);
            }

            plan = AiKeywordMatchers.NormalizePlannerIntentForCompositeTurn(plan, sanitized.Message);
            string mode = AiKeywordMatchers.ModeFromPlan(plan) ?? AiKeywordMatchers.DetermineMode(sanitized.Message, c.ModeCode, sanitized.PageContext?.Domain);
            FasInterviewHandler.ApplyFasTaskInterruptBeforeNonFasTurn(c, sanitized.Message, mode);

            AiHandlerResult result = mode switch
            {
                "PAYMENT" => await paymentHandler.HandleAsync(c, sanitized, plan, ct),
                "FAS_INTERVIEW" => await fasHandler.HandleAsync(c, sanitized, plan, ct),
                _ => await knowledgeHandler.HandleAsync(c, sanitized, plan, ct)
            };
            return await Save(c.Id, pj, now, 0, result, c, sanitized, plan, sw, ct, true);
        }
        catch (ConcurrencyConflictException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Guid rid = await fallbackHandler.CreateReviewAsync(c, c.PersonId, "MODEL_OR_TOOL_FAILURE", sanitized.PageContext, sanitized.Message, now, ct);
            logger.LogError(ex, "AI conv {Id} failed {Elapsed} ms", c.Id, sw.ElapsedMilliseconds);
            AiHandlerResult fb = c.FasSession?.StatusCode switch
            {
                "CANCELLED" => new AiHandlerResult("Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", new(false, []), [], []),
                "PAUSED" => new AiHandlerResult(AiKeywordMatchers.LooksLikeScopeTest(sanitized.Message) ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance." : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.", "GENERAL", new(false, []), [], []),
                _ => fallbackHandler.FallbackResponse(rid)
            };
            var fbr = ToChatResponse(c.Id, 0, fb);
            fbr = FasInterviewHandler.AttachDormantFasState(fbr, c.FasSession);
            var fbm = AiMessage.Create(c.Id, "ASSISTANT", redactor.Redact(fbr.Text), now, latencyMs: (int)sw.ElapsedMilliseconds, responseJson: redactor.Redact(AiResponseBuilder.SerializeResponse(fbr)));
            db.Add(fbm); await db.SaveChangesAsync(ct);
            return fbr with { MessageId = fbm.Id };
        }
    }

    private async Task<AiChatResponse> Save(Guid cid, AiHandlerResult result, AiTurnPlan plan, AiChatRequest req, Stopwatch sw, CancellationToken ct, bool withFasState = false) =>
        await Save(cid, null, DateTime.UtcNow, 0, result, null, req, plan, sw, ct, withFasState);

    private async Task<AiChatResponse> Save(Guid cid, string? pageJson, DateTime now, long mid, AiHandlerResult result, AiConversation? c, AiChatRequest req, AiTurnPlan plan, Stopwatch sw, CancellationToken ct, bool withFasState = false)
    {
        var r = ToChatResponse(cid, mid, result);
        if (withFasState && c is not null) r = FasInterviewHandler.AttachDormantFasState(r, c.FasSession);
        r = AiResponseBuilder.AttachV2Metadata(r, plan);
        r = AiResponseBuilder.AttachFollowUps(r, req);
        if (c is not null && pageJson is not null) c.Touch(r.Mode, pageJson, now);
        var msg = AiMessage.Create(cid, "ASSISTANT", redactor.Redact(r.Text), DateTime.UtcNow, JsonSerializer.Serialize(r.Grounding.Citations, JsonOptions), JsonSerializer.Serialize(r.Cards.Select(x => x.Type), JsonOptions), (int)sw.ElapsedMilliseconds, redactor.Redact(AiResponseBuilder.SerializeResponse(r)));
        db.Add(msg);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex) { throw new ConcurrencyConflictException("FAS session modified by concurrent request", ex); }
        logger.LogInformation("AI conv {Id} mode {Mode} completed {Elapsed} ms", cid, r.Mode, sw.ElapsedMilliseconds);
        return r with { MessageId = msg.Id };
    }

    private async Task<AiConversation> GetOrCreateConversation(Guid? id, long personId, DateTime now, CancellationToken ct)
    {
        if (id.HasValue)
        {
            var existing = await db.Set<AiConversation>().Include(x => x.FasSession).SingleOrDefaultAsync(x => x.Id == id.Value, ct);
            if (existing is not null && existing.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");
            if (existing is not null) return existing;
        }
        var created = AiConversation.Start(id ?? Guid.NewGuid(), personId, now);
        db.Add(created); return created;
    }

    private static AiChatResponse ToChatResponse(Guid cid, long mid, AiHandlerResult r) => new(cid, mid, r.Text, r.Mode, r.Grounding, r.Cards, r.Actions, r.InterviewState, r.ReviewRecordId)
    {
        FollowUpQuestions = r.FollowUpQuestions, TurnIntent = r.TurnIntent, ConversationPhase = r.ConversationPhase
    };
}