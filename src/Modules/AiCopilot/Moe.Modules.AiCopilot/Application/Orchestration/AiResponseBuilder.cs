using System.Text.Json;
using System.Text.RegularExpressions;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class AiResponseBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AiChatResponse AttachV2Metadata(AiChatResponse response, AiTurnPlan plan)
    {
        string phase = response.InterviewState?.Status switch
        {
            "COLLECTING" or "CLARIFYING" => "collecting",
            "CONFIRMING" => "confirming",
            "COMPLETE" => "eligible",
            "PAUSED" => "paused",
            "CANCELLED" => "cancelled",
            "MANUAL_FALLBACK" => "manual_review",
            _ => response.Mode == "PAYMENT" ? "idle" : plan.Phase
        };
        return response with
        {
            Cards = AttachFasTaskStateCard(response.Cards, response.InterviewState, phase),
            TurnIntent = IntentLabel(plan.Intent),
            ConversationPhase = phase
        };
    }

    public static IReadOnlyCollection<AiCard> AttachFasTaskStateCard(IReadOnlyCollection<AiCard> cards, AiInterviewState? interview, string phase)
    {
        if (interview is null || cards.Any(x => x.Type == "FAS_TASK_STATE")) return cards;
        var data = new
        {
            phase,
            status = interview.Status,
            nextQuestion = phase is "paused" or "cancelled" or "manual_review" ? null : interview.NextQuestion,
            statusReason = phase switch
            {
                "paused" => "Paused. Resume when ready.",
                "cancelled" => "Stopped. Restart to calculate eligibility.",
                "manual_review" => "Needs manual form review.",
                _ => null
            },
            resumeLabel = phase == "paused" ? "Resume FAS check" : null,
            restartLabel = phase == "cancelled" ? "Restart FAS check" : null,
            confirmedFacts = interview.Fields.Where(x => x.Confirmed).ToArray(),
            missingFacts = interview.MissingFields,
            formPatch = interview.FormPatch
        };
        return [new("FAS_TASK_STATE", data), .. cards];
    }

    public static string IntentLabel(AiPlannerIntent intent) => intent switch
    {
        AiPlannerIntent.StartFas => "START_FAS",
        AiPlannerIntent.ContinueFas => "CONTINUE_FAS",
        AiPlannerIntent.AnswerKnowledge => "ANSWER_KNOWLEDGE",
        AiPlannerIntent.PaymentQuery => "PAYMENT_QUERY",
        AiPlannerIntent.CourseQuery => "COURSE_QUERY",
        AiPlannerIntent.CancelFas => "CANCEL_FAS",
        AiPlannerIntent.PauseFas => "PAUSE_FAS",
        AiPlannerIntent.SwitchTopic => "SWITCH_TOPIC",
        AiPlannerIntent.OutOfScopeSmallTalk => "OUT_OF_SCOPE_SMALL_TALK",
        AiPlannerIntent.ClarifyFasTypo => "CLARIFY_FAS_TYPO",
        _ => "FALLBACK"
    };

    public static string SerializeResponse(AiChatResponse response)
    {
        var data = new
        {
            response.Mode,
            response.Cards,
            response.Actions,
            response.ReviewRecordId,
            response.InterviewState,
            response.FollowUpQuestions,
            GroundingCards = response.Grounding.Citations,
            GroundingIsGrounded = response.Grounding.IsGrounded,
        };
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static AiChatResponse AttachFollowUps(AiChatResponse response, AiChatRequest request)
    {
        if (response.FollowUpQuestions.Count > 0)
        {
            string[] filtered = FilterCurrentQuestion(response.FollowUpQuestions, request.Message);
            return filtered.Length == response.FollowUpQuestions.Count ? response : response with { FollowUpQuestions = filtered };
        }

        string[] followUps = PlanFollowUps(response.Mode, request.Message, response.InterviewState, []);

        return followUps.Length == 0 ? response : response with { FollowUpQuestions = followUps };
    }

    private static string[] PlanFollowUps(string mode, string message, AiInterviewState? interviewState, IReadOnlyList<KnowledgeResult> sources)
    {
        var planned = new List<string>();
        if (interviewState?.Status is "COLLECTING" or "CLARIFYING")
        {
            planned.Add("Continue my FAS eligibility check.");
        }
        else if (interviewState?.Status == "PAUSED")
        {
            planned.Add("Resume FAS check.");
        }
        else if (interviewState?.Status == "CANCELLED")
        {
            planned.Add("Restart FAS check.");
        }

        planned.AddRange(sources.SelectMany(x => x.FollowUps));

        string[] workflow = mode switch
        {
            "PAYMENT" => PaymentFollowUps(),
            "FALLBACK" => FallbackFollowUps(),
            "GENERAL" when interviewState?.Status == "COMPLETE" => FasCompleteFollowUps(),
            "GENERAL" => AiKeywordMatchers.IsFasQuestion(message) ? FasKnowledgeFollowUps(message) : GeneralFinanceFollowUps(),
            "FAS_INTERVIEW" when interviewState?.Status == "COMPLETE" => FasCompleteFollowUps(),
            _ => []
        };
        planned.AddRange(workflow);

        return FilterCurrentQuestion(planned, message);
    }

    public static string[] FilterCurrentQuestion(IEnumerable<string> followUps, string message)
    {
        string currentQuestion = NormalizeFollowUp(message);
        return followUps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !string.Equals(NormalizeFollowUp(x), currentQuestion, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    public static string NormalizeFollowUp(string value) =>
        Regex.Replace(value.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant);

    private static string[] FasKnowledgeFollowUps(string question)
    {
        string lower = question.ToLowerInvariant();
        if (lower.Contains("pci") || lower.Contains("per capita") || lower.Contains("ghi") || lower.Contains("household income"))
        {
            return
            [
                "What counts as household income?",
                "What documents prove household income?",
                "Which FAS schemes can I apply for?"
            ];
        }

        if (lower.Contains("document"))
        {
            return
            [
                "Walk me through the FAS application process.",
                "Which FAS schemes can I apply for?",
                "How is PCI calculated?"
            ];
        }

        if (lower.Contains("apply") || lower.Contains("process") || lower.Contains("step") || lower.Contains("walk"))
        {
            return
            [
                "What FAS documents do I need?",
                "Which FAS schemes can I apply for?",
                "Check if I qualify for FAS."
            ];
        }

        return
        [
            "Which FAS schemes can I apply for?",
            "What FAS documents do I need?",
            "Walk me through the FAS application process."
        ];
    }

    private static string[] FasCompleteFollowUps() =>
    [
        "What documents do I need before submitting FAS?",
        "Explain how FAS approval works.",
        "What happens after I submit the FAS application?"
    ];

    private static string[] PaymentFollowUps() =>
    [
        "Show my outstanding course bills.",
        "How do I pay this bill?",
        "Show my recent payment history and refunds."
    ];

    private static string[] GeneralFinanceFollowUps() =>
    [
        "What can I use my Education Account for?",
        "How do I top up my Education Account?",
        "What can FAS help pay for?"
    ];

    private static string[] FallbackFollowUps() =>
    [
        "What can you help me with?",
        "How can Admin Center help me?",
        "Show my Education Account balance."
    ];
}
