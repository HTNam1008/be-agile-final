using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiTurnRouter(
    ICurrentUser currentUser,
    AiTurnPlannerService turnPlanner,
    FallbackHandler fallbackHandler,
    PaymentQueryHandler paymentHandler,
    KnowledgeAnswerHandler knowledgeHandler,
    FasInterviewHandler fasHandler,
    ILogger<AiTurnRouter> logger,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = AiJsonOptions.Default;
    private readonly bool _fasEnabled = configuration.GetValue("AiCopilot:FasEnabled", true);

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        var sanitized = new AiChatRequest { ConversationId = request.ConversationId, Message = request.Message.Trim(), PageContext = AiRouterHelpers.SanitizePageContext(request.PageContext), FasState = request.FasState };
        var c = CreateConversation(sanitized.ConversationId, personId, now, sanitized.FasState);
        string? pj = sanitized.PageContext is null ? null : JsonSerializer.Serialize(sanitized.PageContext, JsonOptions);
        var sw = Stopwatch.StartNew();

        try
        {
            // FAS fast-path gate — captures active sessions AND PAUSED/CANCELLED recovery
            // so the agentic path doesn't steal FAS turns.
            bool hasFasSession = _fasEnabled && c.FasSession is not null;
            bool isActiveFas = hasFasSession && c.FasSession!.StatusCode is
                "COLLECTING" or "CONFIRMING" or "CLARIFYING"
                or "COLLECTING_CONFIRMED" or "MANUAL_FALLBACK";
            bool isStoppedFasWithIntent = hasFasSession
                && c.FasSession!.StatusCode is "PAUSED" or "CANCELLED"
                && (FasInterviewHandler.LooksLikeExplicitFasRestart(sanitized.Message)
                    || FasInterviewHandler.LooksLikeContextualResume(sanitized.Message)
                    || AiKeywordMatchers.LooksLikeCancelFas(sanitized.Message));

            bool routeToFas = (isActiveFas || isStoppedFasWithIntent)
                && !AiKeywordMatchers.IsFasKnowledgeInterrupt(sanitized.Message)
                && !AiKeywordMatchers.LooksLikeCapabilityQuestion(sanitized.Message)
                && !AiKeywordMatchers.LooksLikeAdminCenterQuestion(sanitized.Message);

            if (routeToFas)
            {
                var fasPlan = new AiTurnPlan(AiPlannerIntent.ContinueFas, "collecting", null, 1.0m, "ROUTER");
                var fasResult = await fasHandler.HandleAsync(c, sanitized, fasPlan, ct);
                if (fasResult.Signal != HandlerDispatchSignal.None)
                {
                    if (fasResult.Signal == HandlerDispatchSignal.RedirectFallback)
                    {
                        Guid rid = await fallbackHandler.CreateReviewAsync(c, c.PersonId, fasResult.TurnIntent ?? "FAS_MANUAL_FALLBACK", sanitized.PageContext, sanitized.Message, now, ct);
                        var fallbackResp = fallbackHandler.FallbackResponse(rid);
                        var fb = fasResult with { Mode = "FALLBACK", ReviewRecordId = rid, Actions = fallbackResp.Actions, FollowUpQuestions = fasResult.FollowUpQuestions.Count > 0 ? fasResult.FollowUpQuestions : fallbackResp.FollowUpQuestions };
                        return Save(c.Id, pj, now, 0, fb, c, sanitized, fasPlan, sw, ct, true);
                    }
                    var dispatched = fasResult.Signal == HandlerDispatchSignal.RedirectPayment
                        ? await paymentHandler.HandleAsync(c, sanitized, fasPlan, ct)
                        : await knowledgeHandler.HandleAsync(c, sanitized, fasPlan, ct);
                    dispatched = dispatched with { InterviewState = fasResult.InterviewState };
                    return Save(c.Id, pj, now, 0, dispatched, c, sanitized, fasPlan, sw, ct, true);
                }
                return Save(c.Id, pj, now, 0, fasResult, c, sanitized, fasPlan, sw, ct, true);
            }

            // Fast-path deterministic gates (skip agentic path + model planner for simple queries)
            if (AiKeywordMatchers.LooksLikeScopeTest(sanitized.Message))
            {
                var fpPlan = new AiTurnPlan(AiPlannerIntent.OutOfScopeSmallTalk, "idle", null, 0.95m, "FAST_PATH");
                return Save(c.Id, pj, now, 0, await knowledgeHandler.HandleAsync(c, sanitized, fpPlan, ct), c, sanitized, fpPlan, sw, ct, true);
            }
            if (AiKeywordMatchers.LooksLikeCapabilityQuestion(sanitized.Message))
            {
                var fpPlan = new AiTurnPlan(AiPlannerIntent.AnswerKnowledge, "idle", null, 0.95m, "FAST_PATH");
                return Save(c.Id, pj, now, 0, await knowledgeHandler.HandleAsync(c, sanitized, fpPlan, ct), c, sanitized, fpPlan, sw, ct, true);
            }
            if (AiKeywordMatchers.LooksLikeAdminCenterQuestion(sanitized.Message))
            {
                var fpPlan = new AiTurnPlan(AiPlannerIntent.AnswerKnowledge, "idle", null, 0.95m, "FAST_PATH");
                return Save(c.Id, pj, now, 0, await knowledgeHandler.HandleAsync(c, sanitized, fpPlan, ct), c, sanitized, fpPlan, sw, ct, true);
            }
            if (AiKeywordMatchers.LooksLikePaymentQuery(sanitized.Message))
            {
                var fpPlan = new AiTurnPlan(AiPlannerIntent.PaymentQuery, "idle", null, 0.95m, "FAST_PATH");
                return Save(c.Id, pj, now, 0, await paymentHandler.HandleAsync(c, sanitized, fpPlan, ct), c, sanitized, fpPlan, sw, ct, true);
            }
            if (AiKeywordMatchers.LooksLikeCourseQuestion(sanitized.Message))
            {
                var fpPlan = new AiTurnPlan(AiPlannerIntent.CourseQuery, "idle", null, 0.85m, "FAST_PATH");
                return Save(c.Id, pj, now, 0, await knowledgeHandler.HandleAsync(c, sanitized, fpPlan, ct), c, sanitized, fpPlan, sw, ct, true);
            }

            // Deterministic path: planner + mode dispatch. Hardcoded RAG stays ahead of model-led tool loops.
            var plan = await turnPlanner.PlanAsync(sanitized, c, ct);

            if (plan.Intent == AiPlannerIntent.ClarifyFasTypo)
            {
                c.Touch("GENERAL", pj, now);
                return Save(c.Id, new AiHandlerResult("Did you mean FAS? I can help check eligibility.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")])
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
            return Save(c.Id, pj, now, 0, result, c, sanitized, plan, sw, ct, true);
        }
        catch (ConcurrencyConflictException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AI conv {Id} failed after {Elapsed}ms", c.Id, sw.ElapsedMilliseconds);

            Guid? reviewId = null;
            try
            {
                reviewId = await fallbackHandler.CreateReviewAsync(
                    c, c.PersonId, "MODEL_OR_TOOL_FAILURE",
                    sanitized.PageContext, sanitized.Message, now, ct);
            }
            catch (Exception reviewEx)
            {
                logger.LogError(reviewEx, "Fallback review creation failed for conv {Id}", c.Id);
            }

            AiHandlerResult fb;
            if (_fasEnabled && c.FasSession?.StatusCode == "CANCELLED")
            {
                fb = new AiHandlerResult("Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", new(false, []), [], []);
            }
            else if (_fasEnabled && c.FasSession?.StatusCode == "PAUSED")
            {
                fb = new AiHandlerResult(AiKeywordMatchers.LooksLikeScopeTest(sanitized.Message) ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance." : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.", "GENERAL", new(false, []), [], []);
            }
            else
            {
                fb = reviewId.HasValue
                    ? fallbackHandler.FallbackResponse(reviewId.Value)
                    : new AiHandlerResult(
                        "Ask AI is temporarily unavailable. Payments and forms work normally.",
                        "GENERAL", new(false, []), [], []);
            }

            var fbr = ToChatResponse(c.Id, 0, fb);
            if (_fasEnabled)
            {
                fbr = FasInterviewHandler.AttachDormantFasState(fbr, c.FasSession);
            }

            return fbr;
        }
    }

    private AiChatResponse Save(Guid cid, AiHandlerResult result, AiTurnPlan plan, AiChatRequest req, Stopwatch sw, CancellationToken ct, bool withFasState = false) =>
        Save(cid, null, DateTime.UtcNow, 0, result, null, req, plan, sw, ct, withFasState);

    private AiChatResponse Save(Guid cid, string? pageJson, DateTime now, long mid, AiHandlerResult result, AiConversation? c, AiChatRequest req, AiTurnPlan plan, Stopwatch sw, CancellationToken ct, bool withFasState = false)
    {
        var r = ToChatResponse(cid, mid, result);
        if (_fasEnabled && withFasState && c is not null) r = FasInterviewHandler.AttachDormantFasState(r, c.FasSession);
        r = AiResponseBuilder.AttachV2Metadata(r, plan);
        r = AiResponseBuilder.AttachFollowUps(r, req);
        if (c is not null && pageJson is not null) c.Touch(r.Mode, pageJson, now);
        logger.LogInformation("AI conv {Id} mode {Mode} completed {Elapsed} ms", cid, r.Mode, sw.ElapsedMilliseconds);
        return r;
    }

    private static AiConversation CreateConversation(Guid? id, long personId, DateTime now, FasInterviewData? fasState)
    {
        var conversation = AiConversation.Start(id ?? Guid.NewGuid(), personId, now);
        if (fasState is not null)
        {
            conversation.FasSession = AiFasSession.Create(conversation.Id, now);
            FasInterviewHandler.SaveFasState(conversation, fasState, now);
        }
        return conversation;
    }

    private static AiChatResponse ToChatResponse(Guid cid, long mid, AiHandlerResult r) => new(cid, mid, r.Text, r.Mode, r.Grounding, r.Cards, r.Actions, r.InterviewState, r.ReviewRecordId)
    {
        FollowUpQuestions = r.FollowUpQuestions, TurnIntent = r.TurnIntent, ConversationPhase = r.ConversationPhase
    };
}
