using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiTurnPlannerService(
    IConfiguration configuration,
    Kernel kernel,
    ILogger<AiTurnPlannerService> logger)
{
    // ── Pre-compiled regex patterns (CA1869 fix) ───────────────────────────
    private static readonly Regex FasTypoPattern = new(
        @"\b(fss|fass|fs)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FasTypoActionPattern = new(
        @"\b(do|doing|apply|help|feel|start|check)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SmallTalkPattern = new(
        @"\b(tell me (a )?joke|make me laugh|sing|poem|roleplay|story|weather|recipe|movie)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PaymentQueryPattern = new(
        @"\b(PAY(?:MENT|ABLE|ING|S)?|BILL(?:S|ING)?|BALANCE|OUTSTANDING|REFUND|WITHDRAW(?:AL)?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EducationAccountContextPattern = new(
        @"\b(USE|USED|FOR|COVER|PAY)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CourseExactPattern = new(
        @"^\s*(courses?|course\?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CourseWordPattern = new(
        @"\b(course|courses|enrolment|enrollment|class|classes)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CancelFasActionPattern = new(
        @"\b(stop|cancel|quit|end|drop|don't want|dont want|do not want|no longer|not doing|forget)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CancelFasContextPattern = new(
        @"\b(fas|financial assistance|eligibility|check|application|this|anymore|now)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PauseFasPattern = new(
        @"\b(ask something else|something else|different question|change topic|another question)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StartFasKeywordPattern = new(
        @"\b(fas|financial assistance)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StartFasActionPattern = new(
        @"\b(feel|do|doing|help|start|apply|check|qualif|eligib|which|want|need|guide|question)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeWhichPattern = new(
        @"\b(which|what)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeWhatPattern = new(
        @"\b(schemes?|fas|financial assistance|bursary|subsidy)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeEligibPattern = new(
        @"\b(can i apply|apply for|eligible|qualify|available to me|for me)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FasKnowledgeKeywordPattern = new(
        @"\b(fas|financial assistance|pci|per capita|ghi|household income|documents?|scheme|schemes|approval|submit|submitting|application)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FasKnowledgeIntentPattern = new(
        @"\b(what|how|why|explain|calculate|calculated|mean|means|need|prove|happens|after|before)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NaturalAidHelpPattern = new(
        @"\b(help|support|aid|assistance|subsidy|bursary)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NaturalAidContextPattern = new(
        @"\b(school fees?|course fees?|education costs?|school costs?|fees?|family|household|income|earn|afford)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortAnswerExactPattern = new(
        @"^\s*(yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortAnswerPossessivePattern = new(
        @"\b(my|the|our|i am|i'm|its)\b.{0,40}\b(\d[\d,]*(?:\.\d+)?|singapore(?:an)?|foreigner|permanent resident|pr)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ShortAnswerAffirmPattern = new(
        @"^\s*(yes please|no thanks|that's right|that is right|correct|that's correct|yep|nope|sure|confirmed)\s*\.?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    // ───────────────────────────────────────────────────────────────────────
    internal async Task<AiTurnPlan> PlanAsync(AiChatRequest request, AiConversation conversation, CancellationToken ct)
    {
        if (!configuration.GetValue("AiCopilot:PlannerV2Enabled", true))
            return HeuristicPlan(request.Message, conversation);

        if (configuration.GetValue("AiCopilot:PlannerV2UseModel", true))
        {
            AiTurnPlan? modelPlan = await TryModelPlan(request, conversation, ct);
            if (modelPlan is not null)
                return modelPlan;
        }

        return HeuristicPlan(request.Message, conversation);
    }

    private async Task<AiTurnPlan?> TryModelPlan(AiChatRequest request, AiConversation conversation, CancellationToken ct)
    {
        try
        {
            var history = new ChatHistory(
                "You classify one MOE Student Finance copilot turn. Return compact JSON only with keys: intent, phase, confidence, answerGoal. " +
                "Allowed intents: START_FAS, CONTINUE_FAS, ANSWER_KNOWLEDGE, PAYMENT_QUERY, COURSE_QUERY, CANCEL_FAS, PAUSE_FAS, SWITCH_TOPIC, OUT_OF_SCOPE_SMALL_TALK, CLARIFY_FAS_TYPO, FALLBACK. " +
                "The assistant is bounded to student finance, Education Account, bills, payments, refunds, courses, and FAS. " +
                "Classify human intent and conversation control only. Do not calculate money, eligibility, validate fields, or invent facts. " +
                "If the user stops or pauses FAS and asks a finance/course question in the same message, choose PAYMENT_QUERY or COURSE_QUERY so the assistant can answer after stopping the task. " +
                "RULE: When fasPhase is collecting, confirming, or clarifying, ANY numeric value (3000, 4, 0, 5k), yes/no answer, nationality (Singaporean, PR, Foreigner), or employment status MUST be CONTINUE_FAS with confidence >= 0.85. " +
                "RULE: When fasPhase is collecting and the message is a short direct answer (under 80 chars, no question mark, no topic change keyword), classify as CONTINUE_FAS. " +
                "RULE: A question about FAS policy or schemes (what is PCI, how does it work, which schemes) while fasPhase=collecting is ANSWER_KNOWLEDGE, NOT CONTINUE_FAS. " +
                "Examples: fasPhase=collecting, message='3000' -> CONTINUE_FAS 0.95. fasPhase=collecting, message='4' -> CONTINUE_FAS 0.95. fasPhase=collecting, message='Singapore Citizen' -> CONTINUE_FAS 0.95. fasPhase=confirming, message='yes' -> CONTINUE_FAS 0.98. fasPhase=confirming, message='actually 2500 and PR' -> CONTINUE_FAS 0.92. fasPhase=collecting, message='what is PCI?' -> ANSWER_KNOWLEDGE 0.9. message='show my bills' -> PAYMENT_QUERY 0.95.");
            history.AddUserMessage(
                $"currentMode={conversation.ModeCode}; fasPhase={Phase(conversation)}; hasFasState={conversation.FasSession is not null}; " +
                $"route={request.PageContext?.Path}; domain={request.PageContext?.Domain}; message={request.Message}");
            ChatMessageContent answer = await kernel.GetRequiredService<IChatCompletionService>()

                .GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
            string json = answer.Content?.Trim() ?? "";
            Match intent = Regex.Match(json, "\"intent\"\\s*:\\s*\"(?<value>[A-Z_]+)\"", RegexOptions.IgnoreCase);
            Match phase = Regex.Match(json, "\"phase\"\\s*:\\s*\"(?<value>[a-z_]+)\"", RegexOptions.IgnoreCase);
            Match goal = Regex.Match(json, "\"answerGoal\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
            Match confidence = Regex.Match(json, "\"confidence\"\\s*:\\s*(?<value>0?\\.\\d+|1(?:\\.0+)?)", RegexOptions.IgnoreCase);
            if (!intent.Success || !TryParseIntent(intent.Groups["value"].Value, out AiPlannerIntent intentValue))
                return null;
            decimal confidenceValue = confidence.Success && decimal.TryParse(confidence.Groups["value"].Value, out decimal parsed) ? parsed : 0.6m;
            if (confidenceValue < 0.55m)
                return null;
            return new AiTurnPlan(intentValue, phase.Success ? phase.Groups["value"].Value : "idle", goal.Success ? goal.Groups["value"].Value : null, confidenceValue, "MODEL");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI planner v2 model classification failed; falling back to deterministic planner.");
            return null;
        }
    }

    private static AiTurnPlan HeuristicPlan(string message, AiConversation conversation)
    {
        string value = message.Trim();
        string upper = value.ToUpperInvariant();
        bool hasFasState = conversation.FasSession is not null;

        if (LooksLikeFasTypo(value))
            return new(AiPlannerIntent.ClarifyFasTypo, Phase(conversation), "clarify whether the user meant FAS", 0.9m, "HEURISTIC");
        if (LooksLikeOutOfScopeSmallTalk(value))
            return new(AiPlannerIntent.OutOfScopeSmallTalk, Phase(conversation), "decline out-of-scope small talk and offer student-finance help", 0.95m, "HEURISTIC");
        if (hasFasState && LooksLikeCancelFas(value) && LooksLikePaymentQuery(upper))
            return new(AiPlannerIntent.PaymentQuery, Phase(conversation), "stop the active FAS task and answer the finance question", 0.95m, "HEURISTIC");
        if (hasFasState && LooksLikeCancelFas(value) && LooksLikeCourseQuery(value))
            return new(AiPlannerIntent.CourseQuery, Phase(conversation), "stop the active FAS task and answer the course question", 0.9m, "HEURISTIC");
        if (hasFasState && LooksLikeCancelFas(value))
            return new(AiPlannerIntent.CancelFas, Phase(conversation), "stop the active FAS task", 0.95m, "HEURISTIC");
        if (hasFasState && LooksLikePauseFas(value))
            return new(AiPlannerIntent.PauseFas, Phase(conversation), "pause the active FAS task and let the user ask another question", 0.9m, "HEURISTIC");
        if (LooksLikePaymentQuery(upper))
            return new(AiPlannerIntent.PaymentQuery, Phase(conversation), "answer with authorized finance tools", 0.95m, "HEURISTIC");
        if (LooksLikeCourseQuery(value))
            return new(AiPlannerIntent.CourseQuery, Phase(conversation), "answer course-related student finance guidance", 0.85m, "HEURISTIC");
        if (LooksLikeStartFas(value))
            return new(hasFasState ? AiPlannerIntent.ContinueFas : AiPlannerIntent.StartFas, Phase(conversation), "start or resume FAS assistance", 0.9m, "HEURISTIC");
        if ((LooksLikeFasKnowledge(value, hasFasState) || LooksLikeNaturalFasAidQuestion(value)) && !LooksLikeLiveSchemeEligibility(value))
            return new(AiPlannerIntent.AnswerKnowledge, Phase(conversation), "answer FAS knowledge question", 0.9m, "HEURISTIC");
        if (hasFasState && LooksLikeShortAnswer(value))
            return new(AiPlannerIntent.ContinueFas, Phase(conversation), "continue FAS fact collection", 0.8m, "HEURISTIC");

        return new(AiPlannerIntent.Fallback, Phase(conversation), null, 0.5m, "HEURISTIC");
    }

    private static string Phase(AiConversation conversation)
    {
        string? statusCode = conversation.FasSession?.StatusCode;
        if (string.IsNullOrWhiteSpace(statusCode)) return "idle";
        return statusCode.ToUpperInvariant() switch
        {
            "IDLE" => "idle",
            "COMPLETE" => "eligible",
            "CONFIRMING" => "confirming",
            "PAUSED" => "paused",
            "CANCELLED" => "cancelled",
            "MANUAL_FALLBACK" => "manual_review",
            "COLLECTING_CONFIRMED" => "confirming",
            _ => "collecting"
        };
    }

    private static bool LooksLikeFasTypo(string value) =>
        FasTypoPattern.IsMatch(value) && FasTypoActionPattern.IsMatch(value);

    private static bool LooksLikePaymentQuery(string upper) =>
        PaymentQueryPattern.IsMatch(upper) ||
        (upper.Contains("EDUCATION ACCOUNT") && EducationAccountContextPattern.IsMatch(upper));

    private static bool LooksLikeCourseQuery(string value) =>
        CourseExactPattern.IsMatch(value) || CourseWordPattern.IsMatch(value);

    private static bool LooksLikeCancelFas(string value) =>
        CancelFasActionPattern.IsMatch(value) && CancelFasContextPattern.IsMatch(value);

    private static bool LooksLikePauseFas(string value) =>
        PauseFasPattern.IsMatch(value);

    private static bool LooksLikeOutOfScopeSmallTalk(string value) =>
        SmallTalkPattern.IsMatch(value);

    private static bool LooksLikeStartFas(string value) =>
        StartFasKeywordPattern.IsMatch(value) && StartFasActionPattern.IsMatch(value);

    private static bool LooksLikeLiveSchemeEligibility(string value) =>
        LiveSchemeWhichPattern.IsMatch(value) &&
        LiveSchemeWhatPattern.IsMatch(value) &&
        LiveSchemeEligibPattern.IsMatch(value);

    private static bool LooksLikeFasKnowledge(string value, bool hasFasState) =>
        FasKnowledgeKeywordPattern.IsMatch(value) && FasKnowledgeIntentPattern.IsMatch(value);

    private static bool LooksLikeNaturalFasAidQuestion(string value) =>
        NaturalAidHelpPattern.IsMatch(value) && NaturalAidContextPattern.IsMatch(value);

    private static bool LooksLikeShortAnswer(string value) =>
        value.Length < 80 &&
        (ShortAnswerExactPattern.IsMatch(value) ||
         ShortAnswerPossessivePattern.IsMatch(value) ||
         ShortAnswerAffirmPattern.IsMatch(value));


    private static bool TryParseIntent(string value, out AiPlannerIntent intent)
    {
        string normalized = value.Trim().Replace("_", "", StringComparison.OrdinalIgnoreCase);
        foreach (AiPlannerIntent candidate in Enum.GetValues<AiPlannerIntent>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                intent = candidate;
                return true;
            }
        }

        intent = AiPlannerIntent.Fallback;
        return false;
    }
}

public enum AiPlannerIntent
{
    StartFas,
    ContinueFas,
    AnswerKnowledge,
    PaymentQuery,
    CourseQuery,
    CancelFas,
    PauseFas,
    SwitchTopic,
    OutOfScopeSmallTalk,
    ClarifyFasTypo,
    Fallback
}

public sealed record AiTurnPlan(AiPlannerIntent Intent, string Phase, string? AnswerGoal, decimal Confidence, string Source);
