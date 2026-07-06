using System.Text.RegularExpressions;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class AiKeywordMatchers
{
    private static readonly Regex PaymentPattern = new(@"\b(PAY(?:MENT|ABLE|ING|S)?|BILL(?:S|ING)?|BALANCE(?:S)?|OUTSTANDING|REFUND(?:S)?|WITHDRAW(?:AL)?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PaymentContextPattern = new(@"\b(USE|USED|FOR|COVER|PAY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CourseExactPattern = new(@"^\s*(courses?|course\?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CourseWordPattern = new(@"\b(course|courses|enrolment|enrollment|class|classes)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CancelActionPattern = new(@"\b(stop|cancel|quit|end|drop|don't want|dont want|do not want|no longer|not doing|forget)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CancelContextPattern = new(@"\b(fas|financial assistance|eligibility|check|application|this|anymore|now)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SwitchTopicPattern = new(@"\b(ask something else|something else|different question|change topic|another question)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ScopeTestPattern = new(@"\b(tell me (a )?joke|make me laugh|sing|poem|roleplay|story|weather|recipe|movie)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CapabilityPattern = new(@"\b(what can you help|what do you do|help me with|your capabilities|what can i ask)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AdminCenterPattern = new(@"\b(how can admin center help|what can admin center do|admin center help)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NaturalFasHelpPattern = new(@"\b(help|support|aid|assistance|subsidy|bursary)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NaturalFasContextPattern = new(@"\b(school fees?|course fees?|education costs?|school costs?|fees?|family|household|income|earn|afford)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KbInfoIntentPattern = new(@"\b(EXPLAIN|WHAT IS|WHAT ARE|HOW DOES|TELL ME ABOUT|DESCRIBE|OVERVIEW|DETAIL|DETAILS|INFO|INFORMATION|DOCUMENT|DOCUMENTS|WHICH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KbProcessIntentPattern = new(@"\b(WALK ME THROUGH|STEPS?|PROCESS|HOW DO I APPLY|HOW TO APPLY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KbLiveAssessmentPattern = new(@"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|DO I|AM I|I WANT|I NEED|HELP ME APPLY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeWhichPattern = new(@"\b(WHICH|WHAT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeWhatPattern = new(@"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LiveSchemeEligibPattern = new(@"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KnowledgeQuestionPattern = new(@"\b(WHAT|HOW|WHY|EXPLAIN|CALCULAT|MEAN|MEANS|COUNT|COUNTS|DOCUMENT|DOCUMENTS|DEADLINE|PROCESS|STEP|STEPS|REQUIREMENT|REQUIREMENTS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KnowledgeSpecificPattern = new(@"\b(PCI|PER CAPITA|GHI|GROSS HOUSEHOLD|HOUSEHOLD INCOME|INCOME CALCULATION|DOCUMENTS?|BURSARY|SUBSIDY|DEADLINE|PROCESS|STEPS?|REQUIREMENTS?|SCHEMES?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KnowledgeLiveAssessPattern = new(@"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|APPLY|APPLICATION|HELP ME|GUIDE ME|DO FAS|DO FINANCIAL ASSISTANCE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex KnowledgeFieldValuePattern = new(@"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FieldValueStartPattern = new(@"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)[\s,;.:!?]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ContinueActionPattern = new(@"\b(CONTINUE|RESUME|GO BACK|KEEP GOING|FINISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ContinueContextPattern = new(@"\b(FAS|FINANCIAL ASSISTANCE|ELIGIBILITY|CHECK|APPLICATION|INTERVIEW)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FasInterviewAsksPattern = new(@"\b(APPLY|APPLICATION|CHECK|ELIGIB|QUALIF|ASSESS|START|HELP|GUIDE|WANT|DO|WALK|TELL|SHOW|LEARN|KNOW|ASSIST|HOW|QUESTION|RESET|RESTART|REDO|BLUFF)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        PaymentPattern.IsMatch(value) ||
        (value.Contains("EDUCATION ACCOUNT", StringComparison.OrdinalIgnoreCase) && PaymentContextPattern.IsMatch(value));

    public static bool LooksLikeCourseQuestion(string message) =>
        CourseExactPattern.IsMatch(message) || CourseWordPattern.IsMatch(message);

    public static bool LooksLikeCancelFas(string message) =>
        CancelActionPattern.IsMatch(message) && CancelContextPattern.IsMatch(message);

    public static bool LooksLikeSwitchTopic(string message) =>
        SwitchTopicPattern.IsMatch(message);

    public static bool LooksLikeScopeTest(string message) =>
        ScopeTestPattern.IsMatch(message);

    public static bool LooksLikeCapabilityQuestion(string message) =>
        !IsFasQuestion(message) &&
        !message.Contains("FAS", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("financial assistance", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("bursary", StringComparison.OrdinalIgnoreCase) &&
        !message.Contains("subsidy", StringComparison.OrdinalIgnoreCase) &&
        CapabilityPattern.IsMatch(message);

    public static bool LooksLikeAdminCenterQuestion(string message) =>
        AdminCenterPattern.IsMatch(message);

    public static bool LooksLikeNaturalFasAidQuestion(string value) =>
        NaturalFasHelpPattern.IsMatch(value) && NaturalFasContextPattern.IsMatch(value);

    public static bool IsFasQuestion(string message)
    {
        int signals = 0;
        if (message.Contains("FAS", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("financial assistance", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("bursary", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("subsidy", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("scheme", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("PCI", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("per capita", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("GHI", StringComparison.OrdinalIgnoreCase)) signals++;
        if (message.Contains("household income", StringComparison.OrdinalIgnoreCase)) signals++;
        if (LooksLikeNaturalFasAidQuestion(message)) signals++;
        return signals >= 2;
    }

    public static bool IsSchemeKbRequest(string value)
    {
        bool isInfoIntent = KbInfoIntentPattern.IsMatch(value);
        bool isProcessInfoIntent = KbProcessIntentPattern.IsMatch(value);
        bool startsLiveAssessment = KbLiveAssessmentPattern.IsMatch(value);
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE") ||
            value.Contains("BURSARY") || value.Contains("SUBSIDY") || value.Contains("SCHEME");
        return mentionsFas && (isInfoIntent || isProcessInfoIntent) && (!startsLiveAssessment || isProcessInfoIntent);
    }

    public static bool IsLiveSchemeEligibilityRequest(string value) =>
        LiveSchemeWhichPattern.IsMatch(value) &&
        LiveSchemeWhatPattern.IsMatch(value) &&
        LiveSchemeEligibPattern.IsMatch(value);

    internal static readonly Regex CompoundFieldValuePattern = new(
        @"\b(yes|no|y|n|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\b|\d[\d,]*(?:\.\d+)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LeadingClauseQuestionPattern = new(
        @"\b(what|how|why|explain|does|do)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsFasKnowledgeInterrupt(string value)
    {
        bool asksQuestion = KnowledgeQuestionPattern.IsMatch(value) || value.Contains('?');
        bool mentionsSpecificFasKnowledge = KnowledgeSpecificPattern.IsMatch(value);
        bool startsLiveAssessment = KnowledgeLiveAssessPattern.IsMatch(value);
        bool submitsLikelyFieldValue = KnowledgeFieldValuePattern.IsMatch(value);
        bool messageStartsWithFieldValue = FieldValueStartPattern.IsMatch(value);
        bool leadingClauseHasFieldValue = false;
        if (asksQuestion && mentionsSpecificFasKnowledge && !submitsLikelyFieldValue && !messageStartsWithFieldValue)
        {
            var questionMatch = LeadingClauseQuestionPattern.Match(value);
            string leading = questionMatch.Success ? value[..questionMatch.Index] : value;
            leadingClauseHasFieldValue = CompoundFieldValuePattern.IsMatch(leading);
        }
        return asksQuestion && mentionsSpecificFasKnowledge && !startsLiveAssessment && !submitsLikelyFieldValue && !messageStartsWithFieldValue && !leadingClauseHasFieldValue;
    }

    public static AiTurnPlan NormalizePlannerIntentForCompositeTurn(AiTurnPlan plan, string message)
    {
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikePaymentQuery(message))
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
        string value = string.IsNullOrEmpty(domain) ? message : $"{domain} {message}";
        string msgOnly = message;

        if (IsLiveSchemeEligibilityRequest(msgOnly)) return AiTurnIntent.StartInterview;
        if (IsFasInterviewRequest(value)) return AiTurnIntent.StartInterview;
        if (current != "FAS_INTERVIEW" && (IsSchemeKbRequest(msgOnly) || LooksLikeNaturalFasAidQuestion(msgOnly)))
            return AiTurnIntent.AnswerKnowledgeQuestion;
        if (IsFasKnowledgeInterrupt(msgOnly)) return AiTurnIntent.AnswerKnowledgeQuestion;
        if (current == "FAS_INTERVIEW" && IsContinueInterviewRequest(msgOnly))
            return AiTurnIntent.ContinueInterview;
        if (current == "FAS_INTERVIEW" && IsLikelyInterviewAnswer(msgOnly))
            return AiTurnIntent.SubmitInterviewAnswer;
        if (current == "FAS_INTERVIEW") return AiTurnIntent.SubmitInterviewAnswer;

        if (string.Equals(domain, "PAYMENT", StringComparison.OrdinalIgnoreCase) || LooksLikePaymentQuery(msgOnly)) return AiTurnIntent.PaymentQuery;
        if (LooksLikeCapabilityQuestion(message) || LooksLikeAdminCenterQuestion(message))
            return AiTurnIntent.AnswerKnowledgeQuestion;
        return AiTurnIntent.Fallback;
    }

    private static bool IsContinueInterviewRequest(string value) =>
        ContinueActionPattern.IsMatch(value) && ContinueContextPattern.IsMatch(value);

    private static bool IsLikelyInterviewAnswer(string value) =>
        KnowledgeFieldValuePattern.IsMatch(value);

    private static bool IsFasInterviewRequest(string value)
    {
        bool mentionsFas = value.Contains("FAS", StringComparison.OrdinalIgnoreCase) || value.Contains("FINANCIAL ASSISTANCE", StringComparison.OrdinalIgnoreCase);
        bool asksForInterview = FasInterviewAsksPattern.IsMatch(value);
        return (value.Contains("ELIGIB", StringComparison.OrdinalIgnoreCase) || value.Contains("QUALIF", StringComparison.OrdinalIgnoreCase)) && mentionsFas || (mentionsFas && asksForInterview);
    }
}
