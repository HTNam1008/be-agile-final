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
                "Short slot answers like yes, no, 3000, 4, 0, Singaporean, PR, Foreigner continue FAS only when a FAS task is active. " +
                "When fasPhase is collecting, confirming, or clarifying, treat any affirmation, correction, or direct answer as CONTINUE_FAS unless the user explicitly switches topic or cancels.");
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
        Regex.IsMatch(value, @"\b(fss|fass|fs)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(do|doing|apply|help|feel|start|check)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikePaymentQuery(string upper) =>
        upper.Contains("PAY") || upper.Contains("BILL") || upper.Contains("BALANCE") ||
        upper.Contains("OUTSTANDING") || upper.Contains("REFUND") || upper.Contains("WITHDRAW") ||
        (upper.Contains("EDUCATION ACCOUNT") && Regex.IsMatch(upper, @"\b(USE|USED|FOR|COVER|PAY)\b"));

    private static bool LooksLikeCourseQuery(string value) =>
        Regex.IsMatch(value, @"^\s*(courses?|course\?)\s*$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(value, @"\b(course|courses|enrolment|enrollment|class|classes)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeCancelFas(string value) =>
        Regex.IsMatch(value, @"\b(stop|cancel|quit|end|drop|don't want|dont want|do not want|no longer|not doing|forget)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(fas|financial assistance|eligibility|check|application|this|anymore|now)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikePauseFas(string value) =>
        Regex.IsMatch(value, @"\b(ask something else|something else|different question|change topic|another question)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeOutOfScopeSmallTalk(string value) =>
        Regex.IsMatch(value, @"\b(tell me (a )?joke|make me laugh|sing|poem|roleplay|story|weather|recipe|movie)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeStartFas(string value) =>
        Regex.IsMatch(value, @"\b(fas|financial assistance)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(feel|do|doing|help|start|apply|check|qualif|eligib|which|want|need|guide|question)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeLiveSchemeEligibility(string value) =>
        Regex.IsMatch(value, @"\b(which|what)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(schemes?|fas|financial assistance|bursary|subsidy)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(can i apply|apply for|eligible|qualify|available to me|for me)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeFasKnowledge(string value, bool hasFasState) =>
        Regex.IsMatch(value, @"\b(fas|financial assistance|pci|per capita|ghi|household income|documents?|scheme|schemes|approval|submit|submitting|application)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(what|how|why|explain|calculate|calculated|mean|means|need|prove|happens|after|before)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeNaturalFasAidQuestion(string value) =>
        Regex.IsMatch(value, @"\b(help|support|aid|assistance|subsidy|bursary)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(school fees?|course fees?|education costs?|school costs?|fees?|family|household|income|earn|afford)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeShortAnswer(string value) =>
        value.Length < 80 &&
        (Regex.IsMatch(value, @"^\s*(yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$", RegexOptions.IgnoreCase) ||
         Regex.IsMatch(value, @"\b(my|the|our|i am|i'm|its)\b.{0,40}\b(\d[\d,]*(?:\.\d+)?|singapore(?:an)?|foreigner|permanent resident|pr)\b", RegexOptions.IgnoreCase) ||
         Regex.IsMatch(value, @"^\s*(yes please|no thanks|that's right|that is right|correct|that's correct|yep|nope|sure|confirmed)\s*\.?\s*$", RegexOptions.IgnoreCase));

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
