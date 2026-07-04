using System.Text.Json;
using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class AiTurnRouter
{
    private static readonly string[] AllowedDomains = ["FAS", "PAYMENT", "GENERAL"];
    private static readonly string[] AllowedRoutePrefixes =
    [
        "/portal/account",
        "/portal/bills",
        "/portal/courses",
        "/portal/dashboard",
        "/portal/education-account",
        "/portal/fas",
        "/portal/profile"
    ];

    private static readonly HashSet<string> AllowedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "isWelfareHomeResident", "monthlyHouseholdIncome", "householdMemberCount",
        "parentNationalities", "employmentStatusCode", "otherMonthlyIncome", "email"
    };

    public static string DetermineMode(string message, string current, string? domain)
    {
        return ClassifyIntent(message, current, domain) switch
        {
            AiTurnIntent.PaymentQuery => "PAYMENT",
            AiTurnIntent.AnswerKnowledgeQuestion => "GENERAL",
            AiTurnIntent.ContinueInterview or AiTurnIntent.StartInterview or AiTurnIntent.SubmitInterviewAnswer => "FAS_INTERVIEW",
            _ => "GENERAL"
        };
    }

    private static AiTurnIntent ClassifyIntent(string message, string current, string? domain)
    {
        string value = $"{domain} {message}".ToUpperInvariant();
        string msgOnly = message.ToUpperInvariant();
        bool isPaymentDomain = domain?.ToUpperInvariant() == "PAYMENT";
        bool msgHasPaymentKeyword = msgOnly.Contains("PAY") || msgOnly.Contains("BILL") || msgOnly.Contains("BALANCE") || msgOnly.Contains("OUTSTANDING") || msgOnly.Contains("REFUND") || msgOnly.Contains("WITHDRAW");
        if (msgHasPaymentKeyword) return AiTurnIntent.PaymentQuery;
        if (LooksLikeCapabilityQuestion(message) || LooksLikeAdminCenterQuestion(message)) return AiTurnIntent.AnswerKnowledgeQuestion;
        if (IsLiveSchemeEligibilityRequest(msgOnly)) return AiTurnIntent.StartInterview;
        if (current != "FAS_INTERVIEW" && (IsSchemeKbRequest(msgOnly) || LooksLikeNaturalFasAidQuestion(msgOnly))) return AiTurnIntent.AnswerKnowledgeQuestion;
        if (IsFasKnowledgeInterrupt(msgOnly)) return AiTurnIntent.AnswerKnowledgeQuestion;
        if (current == "FAS_INTERVIEW" && IsContinueInterviewRequest(msgOnly)) return AiTurnIntent.ContinueInterview;
        if (current == "FAS_INTERVIEW" && IsLikelyInterviewAnswer(msgOnly)) return AiTurnIntent.SubmitInterviewAnswer;
        if (IsFasInterviewRequest(value)) return AiTurnIntent.StartInterview;
        if (current == "FAS_INTERVIEW") return AiTurnIntent.SubmitInterviewAnswer;
        if (isPaymentDomain) return AiTurnIntent.PaymentQuery;
        return AiTurnIntent.Fallback;
    }

    public static string? ModeFromPlan(AiTurnPlan plan) => plan.Intent switch
    {
        AiPlannerIntent.PaymentQuery => "PAYMENT",
        AiPlannerIntent.AnswerKnowledge or AiPlannerIntent.CourseQuery or AiPlannerIntent.CancelFas or
            AiPlannerIntent.PauseFas or AiPlannerIntent.SwitchTopic or AiPlannerIntent.OutOfScopeSmallTalk => "GENERAL",
        AiPlannerIntent.StartFas or AiPlannerIntent.ContinueFas => "FAS_INTERVIEW",
        _ => null
    };

    public static AiTurnPlan NormalizePlannerIntentForCompositeTurn(AiTurnPlan plan, string message)
    {
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikePaymentQuery(message.ToUpperInvariant()))
            return plan with { Intent = AiPlannerIntent.PaymentQuery, AnswerGoal = "stop the active FAS task and answer the finance question" };
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikeCourseQuestion(message))
            return plan with { Intent = AiPlannerIntent.CourseQuery, AnswerGoal = "stop the active FAS task and answer the course question" };
        return plan;
    }

    public static AiPageContext? SanitizePageContext(AiPageContext? pageContext)
    {
        if (pageContext is null) return null;

        string domain = AllowedDomains.Contains(pageContext.Domain?.ToUpperInvariant())
            ? pageContext.Domain!.ToUpperInvariant()
            : "GENERAL";
        string? path = IsAllowedPath(pageContext.Path) ? pageContext.Path : null;
        string? surface = string.IsNullOrWhiteSpace(pageContext.Surface)
            ? null
            : pageContext.Surface.Length > 80 ? pageContext.Surface[..80] : pageContext.Surface;

        JsonElement? entity = null;
        if (pageContext.Entity.HasValue && domain == "FAS")
        {
            JsonElement e = pageContext.Entity.Value;
            if (e.ValueKind == JsonValueKind.Object &&
                e.TryGetProperty("fieldKey", out JsonElement fk) &&
                fk.ValueKind == JsonValueKind.String &&
                fk.GetString() is string fkStr && AllowedFieldKeys.Contains(fkStr))
            {
                entity = e;
            }
        }

        return new AiPageContext(domain, surface, path, entity);
    }

    public static bool IsAllowedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) return false;
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal)) return false;
        return AllowedRoutePrefixes.Any(prefix =>
            path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase));
    }

    // ── Shared keyword detection ──────────────────────────────────────────

    public static bool LooksLikePaymentQuery(string value) =>
        value.Contains("PAY", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("BILL", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("BALANCE", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("OUTSTANDING", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("REFUND", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("WITHDRAW", StringComparison.OrdinalIgnoreCase) ||
        (value.Contains("EDUCATION ACCOUNT", StringComparison.OrdinalIgnoreCase) &&
         Regex.IsMatch(value, @"\b(USE|USED|FOR|COVER|PAY)\b", RegexOptions.IgnoreCase));

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
        !IsFasQuestion(message) &&
        Regex.IsMatch(message, @"\b(what can you help|what do you do|help me with|your capabilities|what can i ask)\b", RegexOptions.IgnoreCase);

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
        bool isInfoIntent = Regex.IsMatch(value,
            @"\b(EXPLAIN|WHAT IS|WHAT ARE|HOW DOES|TELL ME ABOUT|DESCRIBE|OVERVIEW|DETAIL|DETAILS|INFO|INFORMATION|DOCUMENT|DOCUMENTS|WHICH)\b",
            RegexOptions.IgnoreCase);
        bool isProcessInfoIntent = Regex.IsMatch(value,
            @"\b(WALK ME THROUGH|STEPS?|PROCESS|HOW DO I APPLY|HOW TO APPLY)\b",
            RegexOptions.IgnoreCase);
        bool startsLiveAssessment = Regex.IsMatch(value,
            @"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|DO I|AM I|I WANT|I NEED|HELP ME APPLY)\b",
            RegexOptions.IgnoreCase);
        bool mentionsFas = value.Contains("FAS", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("FINANCIAL ASSISTANCE", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("BURSARY", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("SUBSIDY", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("SCHEME", StringComparison.OrdinalIgnoreCase);
        return mentionsFas && (isInfoIntent || isProcessInfoIntent) && (!startsLiveAssessment || isProcessInfoIntent);
    }

    public static bool IsLiveSchemeEligibilityRequest(string value)
    {
        bool asksWhichSchemes = Regex.IsMatch(value, @"\b(WHICH|WHAT)\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(value, @"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b", RegexOptions.IgnoreCase);
        bool asksApplyOrEligibility = Regex.IsMatch(value, @"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b", RegexOptions.IgnoreCase);
        return asksWhichSchemes && asksApplyOrEligibility;
    }

    public static bool IsFasKnowledgeInterrupt(string value)
    {
        bool asksQuestion = Regex.IsMatch(value, @"\b(WHAT|HOW|WHY|EXPLAIN|CALCULAT|MEAN|MEANS|COUNT|COUNTS|DOCUMENT|DOCUMENTS|DEADLINE|PROCESS|STEP|STEPS|REQUIREMENT|REQUIREMENTS)\b",
            RegexOptions.IgnoreCase) || value.Contains("?", StringComparison.Ordinal);
        bool mentionsSpecificFasKnowledge = Regex.IsMatch(value,
            @"\b(PCI|PER CAPITA|GHI|GROSS HOUSEHOLD|HOUSEHOLD INCOME|INCOME CALCULATION|DOCUMENTS?|BURSARY|SUBSIDY|DEADLINE|PROCESS|STEPS?|REQUIREMENTS?|SCHEMES?)\b",
            RegexOptions.IgnoreCase);
        bool startsLiveAssessment = Regex.IsMatch(value,
            @"\b(CHECK|ELIGIB|QUALIF|ASSESS|START|APPLY|APPLICATION|HELP ME|GUIDE ME|DO FAS|DO FINANCIAL ASSISTANCE)\b",
            RegexOptions.IgnoreCase);
        bool submitsLikelyFieldValue = Regex.IsMatch(value, @"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$",
            RegexOptions.IgnoreCase);

        return asksQuestion && mentionsSpecificFasKnowledge && !startsLiveAssessment && !submitsLikelyFieldValue;
    }

    private static bool IsContinueInterviewRequest(string value) =>
        Regex.IsMatch(value, @"\b(CONTINUE|RESUME|GO BACK|KEEP GOING|FINISH)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(FAS|FINANCIAL ASSISTANCE|ELIGIBILITY|CHECK|APPLICATION|INTERVIEW)\b", RegexOptions.IgnoreCase);

    private static bool IsLikelyInterviewAnswer(string value) =>
        Regex.IsMatch(value, @"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$",
            RegexOptions.IgnoreCase);

    private static bool IsFasInterviewRequest(string value)
    {
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE");
        bool asksForInterview = Regex.IsMatch(value, @"\b(APPLY|APPLICATION|CHECK|ELIGIB|QUALIF|ASSESS|START|HELP|GUIDE|WANT|DO|WALK|TELL|SHOW|LEARN|KNOW|ASSIST|HOW|QUESTION)\b", RegexOptions.IgnoreCase);
        bool eligibilityWithoutFas = (value.Contains("ELIGIB") || value.Contains("QUALIF")) && mentionsFas;
        return eligibilityWithoutFas || (mentionsFas && asksForInterview);
    }

    private enum AiTurnIntent
    {
        AnswerKnowledgeQuestion,
        ContinueInterview,
        StartInterview,
        SubmitInterviewAnswer,
        PaymentQuery,
        Fallback
    }
}
