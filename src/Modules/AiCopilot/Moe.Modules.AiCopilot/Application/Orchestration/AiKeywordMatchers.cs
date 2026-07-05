using System.Text.RegularExpressions;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class AiKeywordMatchers
{
    public static string? ModeFromPlan(AiTurnPlan plan) => plan.Intent switch
    {
        AiPlannerIntent.PaymentQuery => "PAYMENT",
        AiPlannerIntent.AnswerKnowledge or AiPlannerIntent.CourseQuery or
            AiPlannerIntent.CancelFas or AiPlannerIntent.PauseFas or
            AiPlannerIntent.SwitchTopic or AiPlannerIntent.OutOfScopeSmallTalk => "GENERAL",
        AiPlannerIntent.StartFas or AiPlannerIntent.ContinueFas => "FAS_INTERVIEW",
        _ => null
    };

    public static string DetermineMode(string message, string current, string? domain) =>
        ClassifyIntent(message, current, domain) switch
        {
            AiTurnIntent.PaymentQuery => "PAYMENT",
            AiTurnIntent.AnswerKnowledgeQuestion => "GENERAL",
            AiTurnIntent.ContinueInterview or AiTurnIntent.StartInterview or AiTurnIntent.SubmitInterviewAnswer => "FAS_INTERVIEW",
            _ => "GENERAL"
        };

    public static bool LooksLikePaymentQuery(string value) =>
        value.Contains("PAY") || value.Contains("BILL") || value.Contains("BALANCE") ||
        value.Contains("OUTSTANDING") || value.Contains("REFUND") || value.Contains("WITHDRAW") ||
        (value.Contains("EDUCATION ACCOUNT") && Regex.IsMatch(value, @"\b(USE|USED|FOR|COVER|PAY)\b"));

    public static bool LooksLikeCourseQuestion(string message) =>
        Regex.IsMatch(message, @"^\s*(courses?|course\?)\s*$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(message, @"\b(course|courses|enrolment|enrollment|class|classes)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeCancelFas(string message) =>
        Regex.IsMatch(message, @"\b(stop|cancel|quit|end|drop|don't want|dont want|do not want|no longer|not doing|forget)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(fas|financial assistance|eligibility|check|application|this|anymore|now)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeSwitchTopic(string message) =>
        Regex.IsMatch(message, @"\b(ask something else|something else|different question|change topic|another question)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeScopeTest(string message) =>
        Regex.IsMatch(message, @"\b(tell me (a )?joke|make me laugh|sing|poem|roleplay|story|weather|recipe|movie)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeCapabilityQuestion(string message) =>
        !IsFasQuestion(message) && Regex.IsMatch(message, @"\b(what can you help|what do you do|help me with|your capabilities|what can i ask)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeAdminCenterQuestion(string message) =>
        Regex.IsMatch(message, @"\b(how can admin center help|what can admin center do|admin center help)\b", RegexOptions.IgnoreCase);

    public static bool LooksLikeNaturalFasAidQuestion(string value) =>
        Regex.IsMatch(value, @"\b(help|support|aid|assistance|subsidy|bursary)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(school fees?|course fees?|education costs?|school costs?|fees?|family|household|income|earn|afford)\b", RegexOptions.IgnoreCase);

    public static bool IsFasQuestion(string message) =>
        message.Contains("FAS", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("financial assistance", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("bursary", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("subsidy", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("scheme", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("PCI", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("per capita", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("GHI", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("household income", StringComparison.OrdinalIgnoreCase) ||
        LooksLikeNaturalFasAidQuestion(message);

    public static bool IsSchemeKbRequest(string value)
    {
        bool isInfoIntent = Regex.IsMatch(value, @"\b(EXPLAIN|WHAT IS|WHAT ARE|HOW DOES|TELL ME ABOUT|DESCRIBE|OVERVIEW|DETAIL|DETAILS|INFO|INFORMATION|DOCUMENT|DOCUMENTS|WHICH)\b");
        bool isProcessInfoIntent = Regex.IsMatch(value, @"\b(WALK ME THROUGH|STEPS?|PROCESS|HOW DO I APPLY|HOW TO APPLY)\b");
        bool startsLiveAssessment = Regex.IsMatch(value, @"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|DO I|AM I|I WANT|I NEED|HELP ME APPLY)\b");
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE") ||
            value.Contains("BURSARY") || value.Contains("SUBSIDY") || value.Contains("SCHEME");
        return mentionsFas && (isInfoIntent || isProcessInfoIntent) && (!startsLiveAssessment || isProcessInfoIntent);
    }

    public static bool IsLiveSchemeEligibilityRequest(string value) =>
        Regex.IsMatch(value, @"\b(WHICH|WHAT)\b") &&
        Regex.IsMatch(value, @"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b") &&
        Regex.IsMatch(value, @"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b");

    public static bool IsFasKnowledgeInterrupt(string value)
    {
        bool asksQuestion = Regex.IsMatch(value, @"\b(WHAT|HOW|WHY|EXPLAIN|CALCULAT|MEAN|MEANS|COUNT|COUNTS|DOCUMENT|DOCUMENTS|DEADLINE|PROCESS|STEP|STEPS|REQUIREMENT|REQUIREMENTS)\b") ||
            value.Contains("?");
        bool mentionsSpecificFasKnowledge = Regex.IsMatch(value, @"\b(PCI|PER CAPITA|GHI|GROSS HOUSEHOLD|HOUSEHOLD INCOME|INCOME CALCULATION|DOCUMENTS?|BURSARY|SUBSIDY|DEADLINE|PROCESS|STEPS?|REQUIREMENTS?|SCHEMES?)\b");
        bool startsLiveAssessment = Regex.IsMatch(value, @"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|APPLY|APPLICATION|HELP ME|GUIDE ME|DO FAS|DO FINANCIAL ASSISTANCE)\b");
        bool submitsLikelyFieldValue = Regex.IsMatch(value, @"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$");
        return asksQuestion && mentionsSpecificFasKnowledge && !startsLiveAssessment && !submitsLikelyFieldValue;
    }

    public static AiTurnPlan NormalizePlannerIntentForCompositeTurn(AiTurnPlan plan, string message)
    {
        string upper = message.ToUpperInvariant();
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikePaymentQuery(upper))
            return plan with { Intent = AiPlannerIntent.PaymentQuery, AnswerGoal = "stop the active FAS task and answer the finance question" };
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikeCourseQuestion(message))
            return plan with { Intent = AiPlannerIntent.CourseQuery, AnswerGoal = "stop the active FAS task and answer the course question" };
        return plan;
    }

    // ── Private intent classification ────────────────────────────────────

    private enum AiTurnIntent
    {
        AnswerKnowledgeQuestion, ContinueInterview, StartInterview,
        SubmitInterviewAnswer, PaymentQuery, Fallback
    }

    private static AiTurnIntent ClassifyIntent(string message, string current, string? domain)
    {
        string value = $"{domain} {message}".ToUpperInvariant();
        string msgOnly = message.ToUpperInvariant();
        bool isPaymentDomain = domain?.ToUpperInvariant() == "PAYMENT";
        if (msgOnly.Contains("PAY") || msgOnly.Contains("BILL") || msgOnly.Contains("BALANCE") ||
            msgOnly.Contains("OUTSTANDING") || msgOnly.Contains("REFUND") || msgOnly.Contains("WITHDRAW") ||
            (msgOnly.Contains("EDUCATION ACCOUNT") && Regex.IsMatch(msgOnly, @"\b(USE|USED|FOR|COVER|PAY)\b")))
            return AiTurnIntent.PaymentQuery;
        if (LooksLikeCapabilityQuestion(message) || LooksLikeAdminCenterQuestion(message))
            return AiTurnIntent.AnswerKnowledgeQuestion;
        if (IsLiveSchemeEligibilityRequest(msgOnly)) return AiTurnIntent.StartInterview;
        if (current != "FAS_INTERVIEW" && (IsSchemeKbRequest(msgOnly) || LooksLikeNaturalFasAidQuestion(msgOnly)))
            return AiTurnIntent.AnswerKnowledgeQuestion;
        if (IsFasKnowledgeInterrupt(msgOnly)) return AiTurnIntent.AnswerKnowledgeQuestion;
        if (current == "FAS_INTERVIEW" && IsContinueInterviewRequest(msgOnly))
            return AiTurnIntent.ContinueInterview;
        if (current == "FAS_INTERVIEW" && IsLikelyInterviewAnswer(msgOnly))
            return AiTurnIntent.SubmitInterviewAnswer;
        if (IsFasInterviewRequest(value)) return AiTurnIntent.StartInterview;
        if (current == "FAS_INTERVIEW") return AiTurnIntent.SubmitInterviewAnswer;
        if (isPaymentDomain) return AiTurnIntent.PaymentQuery;
        return AiTurnIntent.Fallback;
    }

    private static bool IsContinueInterviewRequest(string value) =>
        Regex.IsMatch(value, @"\b(CONTINUE|RESUME|GO BACK|KEEP GOING|FINISH)\b") &&
        Regex.IsMatch(value, @"\b(FAS|FINANCIAL ASSISTANCE|ELIGIBILITY|CHECK|APPLICATION|INTERVIEW)\b");

    private static bool IsLikelyInterviewAnswer(string value) =>
        Regex.IsMatch(value, @"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$");

    private static bool IsFasInterviewRequest(string value)
    {
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE");
        bool asksForInterview = Regex.IsMatch(value, @"\b(APPLY|APPLICATION|CHECK|ELIGIB|QUALIF|ASSESS|START|HELP|GUIDE|WANT|DO|WALK|TELL|SHOW|LEARN|KNOW|ASSIST|HOW|QUESTION)\b");
        return (value.Contains("ELIGIB") || value.Contains("QUALIF")) && mentionsFas || (mentionsFas && asksForInterview);
    }
}
