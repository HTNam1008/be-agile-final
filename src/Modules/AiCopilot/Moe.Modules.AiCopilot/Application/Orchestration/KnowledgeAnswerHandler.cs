using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class KnowledgeAnswerHandler(
    IKnowledgeRetriever knowledge,
    Kernel kernel,
    FallbackHandler fallback)
{
    private static readonly JsonSerializerOptions JsonOptions = AiJsonOptions.Default;

    // ── Pre-compiled regex patterns (CA1869 fix) ───────────────────────────
    private static readonly Regex InstructionHeadingPattern = new(
        @"^(answer|do not|source|notes?|scope|in scope|explicitly out of scope)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DocumentFactPattern = new(
        @"\b(document|income proof|cpf|iras|payslip|assessment|supporting|attach|declare|rental|dividend|investment)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GhiFormulaPattern = new(
        @"\b(GHI|GROSS HOUSEHOLD INCOME)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GhiCalcPattern = new(
        @"\b(CALCULAT\w*|FORMULA|HOW|WHAT|DEFINE|MEAN)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SubsidyBursaryPattern = new(
        @"\b(SUBSIDY|BURSARY)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SubsidyRatePattern = new(
        @"\b(RATE|CALCULAT\w*|DETERMINE|HOW|FORMULA|TIER)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PciTermPattern = new(
        @"\b(PCI|PER[-\s]?CAPITA|PER CAPITA INCOME)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PciCalcPattern = new(
        @"\b(CALCULAT\w*|FORMULA|HOW|WHAT|MEAN)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HouseholdIncomeWhatPattern = new(
        @"\b(what|which|count|counts|included|include)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HouseholdIncomeContextPattern = new(
        @"\b(household income|income for fas|fas income)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DocumentQuestionPattern = new(
        @"\b(document|documents|proof|payslip|cpf|iras|supporting)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WhitespaceNormalizePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BoldMarkerPattern = new(
        @"\*{1,2}",
        RegexOptions.Compiled);
    private static readonly Regex SentenceSplitPattern = new(
        @"(?<=[.!?])\s+",
        RegexOptions.Compiled);
    private static readonly Regex HeadingStripPattern = new(
        @"^[#*\-\s|>]+",
        RegexOptions.Compiled);
    private static readonly Regex NormalizeFollowUpPattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    // ───────────────────────────────────────────────────────────────────────

    public async Task<AiHandlerResult> HandleAsync(AiConversation conversation, AiChatRequest request, AiTurnPlan plan, CancellationToken ct)
    {
        if (AiKeywordMatchers.LooksLikeScopeTest(request.Message))
        {
            return new AiHandlerResult(
                "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.",
                "GENERAL", new(false, []), [], [])
            {
                FollowUpQuestions =
                [
                    "What can you help me with?",
                    "Check if I qualify for FAS.",
                    "Show my Education Account balance."
                ]
            };
        }

        if (AiKeywordMatchers.LooksLikeCapabilityQuestion(request.Message))
        {
            const string capabilityText = "I can help with Education Account balance, outstanding bills, payment history, refunds, and FAS guidance. I can also walk you through a FAS eligibility check before you open the application form.";
            AiAction[] capabilityActions =
            [
                new("NAVIGATE", "Open Bills & payments page", "/portal/payments"),
                new("NAVIGATE", "Open FAS application", "/portal/fas")
            ];
            return new AiHandlerResult(capabilityText, "GENERAL", new(false, []), [], capabilityActions)
            {
                FollowUpQuestions =
                [
                    "Show my Education Account balance.",
                    "Check if I qualify for FAS.",
                    "What documents do I need for FAS?"
                ]
            };
        }

        if (AiKeywordMatchers.LooksLikeCourseQuestion(request.Message))
        {
            const string courseText = "I can help with course-related finance questions, such as outstanding course bills, payment options, and how FAS may apply to eligible course charges. For course enrolment details, open the Courses page.";
            return new AiHandlerResult(courseText, "GENERAL", new(false, []), [],
                [new("NAVIGATE", "Open Courses page", "/portal/courses"), new("NAVIGATE", "Open Bills & payments page", "/portal/payments")])
            {
                FollowUpQuestions =
                [
                    "Show my outstanding course bills.",
                    "How do I pay this bill?",
                    "Check if I qualify for FAS."
                ]
            };
        }

        if (AiKeywordMatchers.LooksLikeAdminCenterQuestion(request.Message))
        {
            const string adminText = "Admin Center can review questions the copilot cannot answer safely, such as unusual FAS circumstances, disputed bills, refund issues, or application details that need staff judgement.";
            return new AiHandlerResult(adminText, "GENERAL", new(false, []), [],
                [new("CONTACT_ADMIN_CENTER", "Contact Admin Center")])
            {
                FollowUpQuestions =
                [
                    "Check if I qualify for FAS.",
                    "Show my outstanding course bills.",
                    "What can you help me with?"
                ]
            };
        }

        bool isFasKnowledgeRequest = AiKeywordMatchers.IsSchemeKbRequest(request.Message) ||
            AiKeywordMatchers.IsFasKnowledgeInterrupt(request.Message) ||
            AiKeywordMatchers.LooksLikeNaturalFasAidQuestion(request.Message);
        string retrievalDomain = isFasKnowledgeRequest ? "FAS" : request.PageContext?.Domain ?? "GENERAL";
        IReadOnlyList<KnowledgeResult> sources = await knowledge.RetrieveAsync(request.Message, retrievalDomain, ct: ct);
        if (sources.Count == 0)
        {
            Guid review = await fallback.CreateReviewAsync(conversation, conversation.PersonId, "MISSING_POLICY", request.PageContext, request.Message, DateTime.UtcNow, ct);
            return fallback.FallbackResponse(review);
        }

        KnowledgeAnswerCard knowledgeCard = BuildKnowledgeAnswerCard(request.Message, sources);
        string sourceText = string.Join("\n", sources.Select(x => $"[{x.Citation.SourceId}] ({x.Citation.SourceStatus}) {x.Content}"));

        // Formula and document questions get deterministic fast-path answers.
        // All other questions — including FAS scheme info — go through the LLM
        // so the student gets a real synthesised answer, not a generic card-redirect.
        string? fastPathText = null;
        if (isFasKnowledgeRequest)
        {
            if (LooksLikeFormulaQuestion(request.Message))
                fastPathText = FormulaAnswer(request.Message);
            else if (LooksLikeHouseholdIncomeDefinitionQuestion(request.Message))
                fastPathText = "Household income usually means the combined monthly income of household members, including employment income and other regular income such as rental, dividend, or investment income where applicable.";
            else if (LooksLikeDocumentQuestion(request.Message))
            {
                string firstFact = knowledgeCard.Summary;
                fastPathText = string.IsNullOrWhiteSpace(firstFact)
                    ? "Before submitting FAS, prepare the income and supporting documents requested by the institution, then review the form before submission."
                    : $"Before submitting FAS, prepare the requested supporting documents. {firstFact}";
            }
        }

        if (fastPathText is not null)
        {
            return new AiHandlerResult(fastPathText, "GENERAL", Grounding(sources),
                [new("KNOWLEDGE_ANSWER", knowledgeCard)], KnowledgeActions(sources))
            {
                FollowUpQuestions = ContextualKnowledgeFollowUps(conversation, request.Message, knowledgeCard.FollowUpQuestions)
            };
        }

        // LLM-synthesised answer for all grounded questions (FAS scheme info, process, eligibility facts, etc.)
        var history = new ChatHistory(
            "You are the MOE Student Finance Copilot. Answer like a calm counter officer, not a policy document.\n" +
            "Keep the answer under 120 words. Lead with the direct answer. Ask at most one next question.\n" +
            "Use no more than three bullets. Do not include source IDs, bracket citations, or raw document codes in the answer text; the UI renders sources separately.\n" +
            "Never invent personal data, policy, eligibility, amounts, status, or timelines. If the question is outside student finance or FAS, say what you can help with instead.\n" +
            "Label prototype uncertainty in plain language only when it affects the answer.\n" +
            $"Sources:\n{sourceText}");
        history.AddUserMessage(request.Message);
        ChatMessageContent answer = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
        string text = string.IsNullOrWhiteSpace(answer.Content) ? "I do not have enough reliable information to answer that." : answer.Content.Trim();
        if (sources.Any(x => x.Citation.SourceStatus == "PROTOTYPE"))
        {
            text += "\n\nSome parts of this answer are based on prototype guidance and may change. Use the actions below when you want to continue in the portal.";
        }
        return new AiHandlerResult(text, "GENERAL", Grounding(sources),
            [new("KNOWLEDGE_ANSWER", knowledgeCard)], KnowledgeActions(sources))
        {
            FollowUpQuestions = ContextualKnowledgeFollowUps(conversation, request.Message, knowledgeCard.FollowUpQuestions)
        };
    }

    // BuildKnowledgeAnswer is no longer the primary FAS answer path — LLM handles those.
    // Retained for formula/document fast-paths only, called inline above.
    private static string BuildKnowledgeAnswerFallback(string question, IReadOnlyList<KnowledgeResult> sources)
    {
        if (LooksLikeFormulaQuestion(question)) return FormulaAnswer(question);
        if (LooksLikeHouseholdIncomeDefinitionQuestion(question))
            return "Household income usually means the combined monthly income of household members, including employment income and other regular income such as rental, dividend, or investment income where applicable.";
        if (LooksLikeDocumentQuestion(question))
        {
            string firstFact = BuildKnowledgeAnswerCard(question, sources).Summary;
            return string.IsNullOrWhiteSpace(firstFact)
                ? "Before submitting FAS, prepare the income and supporting documents requested by the institution, then review the form before submission."
                : $"Before submitting FAS, prepare the requested supporting documents. {firstFact}";
        }
        // Generic grounded fallback — LLM path should be preferred over this.
        return $"Here is what I found about {sources[0].Citation.Section}. Review the card below for details.";
    }

    private static string FormulaAnswer(string question)
    {
        if (LooksLikePciQuestion(question))
            return "PCI means per-capita income. It is calculated as total monthly household income divided by the number of household members.";
        if (Regex.IsMatch(question, @"\b(GHI|GROSS HOUSEHOLD INCOME)\b", RegexOptions.IgnoreCase))
            return "GHI means gross household income. It includes all income earned by every household member from employment, business, investments, and regular allowances before deductions.";
        if (Regex.IsMatch(question, @"\b(SUBSIDY|BURSARY)\b", RegexOptions.IgnoreCase))
            return "Subsidy and bursary rates are based on per-capita income (PCI) brackets. Each scheme uses MOE-published tier thresholds that determine the subsidy percentage.";
        return "I cannot find a specific formula for that. Please ask about PCI calculation, GHI definition, or bursary tier determination.";
    }

    internal static KnowledgeAnswerCard BuildKnowledgeAnswerCard(string question, IReadOnlyList<KnowledgeResult> sources)
    {
        KnowledgeResult primary = sources[0];
        string[] facts; string summaryHint;
        if (LooksLikeFormulaQuestion(question))
        {
            string answer = FormulaAnswer(question);
            facts = answer.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s + ".").ToArray();
            summaryHint = facts.FirstOrDefault() ?? "";
        }
        else if (LooksLikeHouseholdIncomeDefinitionQuestion(question))
        {
            facts =
            [
                "Household income usually means the combined monthly income of household members.",
                "For employed household members, use income declared to the CPF Board or assumed under CPF Regulations.",
                "Other income can include taxable rental, dividend, and investment income averaged over 12 months based on the latest available IRAS assessment."
            ];
            summaryHint = facts[0];
        }
        else
        {
            facts = SelectKnowledgeFacts(question, primary.Content).Select(StripBoldMarkers).ToArray();
            summaryHint = facts.FirstOrDefault() ?? CleanKnowledgeSnippet(primary.Content);
        }
        string summary = summaryHint;
        string[] keyFacts = facts.Skip(1).DefaultIfEmpty(summary).Take(4).ToArray();
        string[] sourceIds = sources.Select(x => x.Citation.SourceId).Distinct(StringComparer.Ordinal).Take(4).ToArray();
        KnowledgeSourceSummary[] sourceSummaries = sources
            .GroupBy(x => x.Citation.SourceId, StringComparer.Ordinal)
            .Select(group =>
            {
                KnowledgeResult result = group.First();
                return new KnowledgeSourceSummary(
                    result.Citation.SourceId,
                    result.Citation.Title,
                    result.Citation.SourceStatus,
                    result.Citation.EffectiveDate,
                    result.ReviewOwner,
                    result.AllowedIntents);
            })
            .Take(4)
            .ToArray();
        string sourceQuality = sources.Any(x => x.Citation.SourceStatus == "OFFICIAL")
            ? "Official MOE guidance"
            : sources.Any(x => x.Citation.SourceStatus == "GUIDE")
                ? "Reviewed guidance"
                : "Prototype or FAQ guidance";
        string[] followUps = PlanFollowUps("GENERAL", question, null, sources);

        return new KnowledgeAnswerCard(
            primary.Citation.Section,
            summary,
            keyFacts,
            ["Use Open FAS application below to review live schemes and draft status.", "Ask \"Check if I qualify for FAS\" and I can collect the answers before you open the form."],
            sourceIds,
            sourceQuality,
            followUps,
            sourceSummaries,
            string.Join(", ", sources.Select(x => x.Citation.Version).Distinct(StringComparer.Ordinal).Take(3)));
    }

    private static string CleanKnowledgeSnippet(string content)
    {
        string[] lines = KnowledgeLines(content)
            .Take(4)
            .ToArray();
        string text = string.Join(" ", lines);
        text = WhitespaceNormalizePattern.Replace(text, " ").Trim();
        return text.Length <= 360 ? text : $"{text[..360].TrimEnd()}...";
    }

    private static IEnumerable<string> KnowledgeLines(string content) => content.Split('\n')
        .Select(line => HeadingStripPattern.Replace(line.Trim(), "").Trim())
        .Where(line => line.Length > 0 && !line.Contains("---", StringComparison.Ordinal) && !line.StartsWith("|", StringComparison.Ordinal))
        .Select(line => WhitespaceNormalizePattern.Replace(line, " ").Trim())
        .Where(line => line.Length > 0);

    private static bool LooksLikeInstructionHeading(string line) =>
        InstructionHeadingPattern.IsMatch(line);

    private static IEnumerable<string> KnowledgeFacts(string content)
    {
        foreach (string line in KnowledgeLines(content).Where(line => !LooksLikeInstructionHeading(line)))
        {
            foreach (string sentence in SentenceSplitPattern.Split(line))
            {
                string value = sentence.Trim();
                if (value.Length > 0)
                    yield return value;
            }
        }
    }

    private static IEnumerable<string> SelectKnowledgeFacts(string question, string content)
    {
        string lower = question.ToLowerInvariant();
        string[] facts = KnowledgeFacts(content).ToArray();
        if (lower.Contains("document") || lower.Contains("proof") || lower.Contains("payslip") || lower.Contains("cpf") || lower.Contains("iras"))
        {
            string[] documentFacts = facts
                .Where(f => DocumentFactPattern.IsMatch(f))
                .ToArray();
            if (documentFacts.Length > 0)
                return documentFacts;
        }

        return facts;
    }

    private static string StripBoldMarkers(string text) =>
        BoldMarkerPattern.Replace(text, "");

    private static bool LooksLikeFormulaQuestion(string message) =>
        LooksLikePciQuestion(message) ||
        (GhiFormulaPattern.IsMatch(message) && GhiCalcPattern.IsMatch(message)) ||
        (SubsidyBursaryPattern.IsMatch(message) && SubsidyRatePattern.IsMatch(message));

    private static bool LooksLikePciQuestion(string message) =>
        PciTermPattern.IsMatch(message) && PciCalcPattern.IsMatch(message);

    private static bool LooksLikeHouseholdIncomeDefinitionQuestion(string message) =>
        HouseholdIncomeWhatPattern.IsMatch(message) && HouseholdIncomeContextPattern.IsMatch(message);

    private static bool LooksLikeDocumentQuestion(string message) =>
        DocumentQuestionPattern.IsMatch(message);

    private static IReadOnlyList<AiAction> KnowledgeActions(IReadOnlyList<KnowledgeResult> sources)
    {
        bool hasFasSource = sources.Any(source => source.Citation.SourceId.StartsWith("FAS-", StringComparison.OrdinalIgnoreCase));
        return hasFasSource ? [new("NAVIGATE", "Open FAS application", "/portal/fas")] : [];
    }

    private static string[] ContextualKnowledgeFollowUps(AiConversation conversation, string message, IReadOnlyCollection<string> followUps)
    {
        const string resumeInterview = "Continue my FAS eligibility check.";
        bool hasInterruptedInterview = conversation.ModeCode == "FAS_INTERVIEW" && conversation.FasSession is not null;
        string currentQuestion = NormalizeFollowUp(message);
        IEnumerable<string> planned = followUps.Where(x =>
            !string.Equals(x, resumeInterview, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(NormalizeFollowUp(x), currentQuestion, StringComparison.OrdinalIgnoreCase));
        if (hasInterruptedInterview)
        {
            planned = new[] { resumeInterview }.Concat(planned);
        }

        return planned
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

    private static string NormalizeFollowUp(string value) =>
        NormalizeFollowUpPattern.Replace(value.Trim().TrimEnd('.', '?', '!'), " ");

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

    private static string[] FilterCurrentQuestion(IEnumerable<string> followUps, string message)
    {
        string currentQuestion = NormalizeFollowUp(message);
        return followUps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !string.Equals(NormalizeFollowUp(x), currentQuestion, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }

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

    private static AiGrounding Grounding(IReadOnlyList<KnowledgeResult> sources) => new(sources.Count > 0, sources.Select(x => x.Citation).ToArray());
}
