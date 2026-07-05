using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FasInterviewHandler(
    StudentFasApplicationService fas,
    ILogger<FasInterviewHandler> logger,
    FasExtractionService extraction,
    FasEligibilityService eligibility)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static FasInterviewData? LoadFasState(AiConversation c) =>
        c.FasSession?.CollectedFactsJson is { Length: > 0 } json ? JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions) : null;

    public static void SaveFasState(AiConversation c, FasInterviewData state, DateTime now)
    {
        c.FasSession ??= AiFasSession.Create(c.Id, now);
        c.FasSession.StatusCode = state.Status;
        c.FasSession.CollectedFactsJson = JsonSerializer.Serialize(state, JsonOptions);
        c.FasSession.UpdatedAtUtc = now;
    }

    public async Task<AiHandlerResult> HandleAsync(AiConversation c, AiChatRequest request, AiTurnPlan plan, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        bool isNew = c.FasSession is null;
        string? fk = request.PageContext?.Entity is JsonElement e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty("fieldKey", out JsonElement fke) && fke.ValueKind == JsonValueKind.String && fke.GetString() is string fks ? fks : null;
        string? pj = request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        FasInterviewData st;
        try { st = LoadFasState(c) ?? await InitializeFasState(ct); }
        catch { return new("I couldn't read enough profile information from Singpass to help with FAS. You can still use the FAS form directly, or contact Admin Center for assistance.", "FALLBACK", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")]); }

        if (IsTerminalFasState(st.Status))
        {
            if (st.Status == "CANCELLED" && LooksLikeExplicitFasRestart(request.Message)) { st = await InitializeFasState(ct); isNew = true; }
            else if (LooksLikeExplicitFasRestart(request.Message) || LooksLikeContextualResume(request.Message)) { st.Status = ResolveTargetField(st, fk) is null ? "CONFIRMING" : "COLLECTING"; st.ValidationMessage = null; st.ClarificationAttempts.Clear(); }
            else return await HandleStoppedFasTurn(c, request, st, now, ct);
        }

        if (st.Status == "CONFIRMING") { var cr = await HandleConfirmingGate(c, request, st, now, ct); if (cr is not null) return cr; }
        if (!isNew && st.Status == "COMPLETE") return await HandleCompletedFas(c, request, st, pj, now, ct);

        if (AiKeywordMatchers.LooksLikeCancelFas(request.Message))
        {
            st.Status = "CANCELLED"; st.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            var iv = FasConfirmationService.ToInterviewState(st, null); SaveFasState(c, st, now);
            return new("Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], iv) { FollowUpQuestions = ["Show my outstanding course bills.", "Restart FAS check.", "What can you help me with?"] };
        }

        if (AiKeywordMatchers.LooksLikeSwitchTopic(request.Message) || AiKeywordMatchers.LooksLikeScopeTest(request.Message))
        {
            st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused before eligibility calculation.";
            var iv = FasConfirmationService.ToInterviewState(st, null);
            string pt = AiKeywordMatchers.LooksLikeScopeTest(request.Message) ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance." : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new(pt, "GENERAL", new(false, []), [], [], iv) { FollowUpQuestions = ["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"] };
        }

        if (AiKeywordMatchers.LooksLikePaymentQuery(request.Message.ToUpperInvariant()) || AiKeywordMatchers.LooksLikeCourseQuestion(request.Message))
        {
            st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused while answering a side question.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            string redirectMode = AiKeywordMatchers.LooksLikePaymentQuery(request.Message.ToUpperInvariant()) ? "REDIRECT_PAYMENT" : "REDIRECT_KNOWLEDGE";
            return new AiHandlerResult(request.Message, redirectMode, new(false, []), [], [],
                FasConfirmationService.ToInterviewState(st, null))
            {
                FollowUpQuestions = FilterCurrentQuestion(["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"], request.Message)
            };
        }

        bool isGuide = fk is null && LooksLikeFasSchemeGuidanceRequest(request.Message);
        bool isFieldHelp = fk is not null && FasExtractionService.LooksLikeFieldHelpRequest(request.Message);
        bool shouldAsk = (isNew && fk is null) || isGuide || isFieldHelp;
        string? answered = shouldAsk ? null : ResolveTargetField(st, fk);
        var ext = shouldAsk ? FasExtractionResult.Accepted() : await extraction.ApplyFasAnswerWithLlmAsync(st, request.Message, pref => ResolveTargetField(st, pref), fk, ct);

        if (ext.Status == "MANUAL_FALLBACK")
        {
            st.Status = "MANUAL_FALLBACK";
            var miv = FasConfirmationService.ToInterviewState(st, null);
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult(ext.Message!, "REDIRECT_FALLBACK", new(false, []), [], [], miv) { TurnIntent = "FAS_MANUAL_FALLBACK" };
        }

        if (ext.Status == "CLARIFY")
        {
            st.Status = "CLARIFYING";
            var civ = FasConfirmationService.ToInterviewState(st, ext.Message);
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new(ext.Message!, "FAS_INTERVIEW", new(false, []), [], [], civ);
        }

        string? nf = ResolveTargetField(st, fk);
        string? next = NextQuestion(st, fk);
        if (next is not null) st.ClarificationField = nf;
        if (next is not null && st.Status == "COLLECTING_CONFIRMED") st.Status = "COLLECTING";

        object? rec = null;
        FasRecommendationMatch[] schemes = [];
        string text;

        if (next is null && !IsReadyForEligibilityComputation(st)) { st.Status = "CONFIRMING"; text = FasConfirmationService.ConfirmationPrompt(st); }
        else if (next is null && st.IsWelfareHomeResident == false) (text, st, schemes, rec) = await eligibility.ComputeEligibility(st, ct);
        else if (next is null)
        {
            st.Status = "COMPLETE"; schemes = FasEligibilityService.WelfareHomeRecommendationMatches(st); st.RecommendationMatches = schemes.ToList();
            text = schemes.Length > 0 ? $"I have your welfare-home status and parent or guardian nationality. I found {schemes.Length} open FAS scheme{(schemes.Length == 1 ? "" : "s")} for your school and prepared them for the form. Use 'Apply answers to form', then review before submitting." : "I have your welfare-home status and parent or guardian nationality. The FAS form will skip household income and household-size questions. I could not auto-select a scheme, so choose the scheme manually before submitting.";
        }
        else
        {
            st.Status = "COLLECTING";
            string? ack = ext.Status == "ACCEPTED" ? FasExtractionService.AcceptedFieldAcknowledgement(answered, st) : null;
            text = isFieldHelp ? next : shouldAsk ? $"{FasExtractionService.ProfileFactsIntro(st)}\n\n{next}" : ack is null ? next : $"{ack}\n\n{next}";
        }

        var ivw = FasConfirmationService.ToInterviewState(st, next, schemes);
        if (st.Status == "MANUAL_FALLBACK")
        {
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult(text, "REDIRECT_FALLBACK", new(false, []), [], [], ivw) { TurnIntent = "FAS_MANUAL_FALLBACK" };
        }
        string em = st.Status == "COMPLETE" ? "GENERAL" : "FAS_INTERVIEW";
        c.Touch(em, pj, now); SaveFasState(c, st, now);
        List<AiCard> cards = rec is null ? [] : [new("FAS_RECOMMENDATION", rec)];
        List<AiAction> acts = st.Status == "COMPLETE" ? [new("NAVIGATE", "Open FAS application", "/portal/fas", ivw.FormPatch)] : [];
        if (st.Status == "COMPLETE") acts.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: ivw.FormPatch));
        return new(text, em, new(false, []), cards, acts, ivw);
    }

    private async Task<AiHandlerResult?> HandleConfirmingGate(AiConversation c, AiChatRequest req, FasInterviewData st, DateTime now, CancellationToken ct)
    {
        string? pj = req.PageContext is null ? null : JsonSerializer.Serialize(req.PageContext, JsonOptions);

        if (AiKeywordMatchers.LooksLikeCancelFas(req.Message))
        {
            st.Status = "CANCELLED"; st.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            var iv = FasConfirmationService.ToInterviewState(st, null); c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new("Got it. I stopped this FAS check and will not calculate eligibility from those answers. You can restart the FAS check later or open the form manually.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], iv) { FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."] };
        }

        if (AiKeywordMatchers.LooksLikeScopeTest(req.Message) || AiKeywordMatchers.LooksLikeSwitchTopic(req.Message))
        {
            st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused before eligibility calculation.";
            var iv = FasConfirmationService.ToInterviewState(st, null);
            string pt = AiKeywordMatchers.LooksLikeScopeTest(req.Message) ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance." : "No problem. I paused this FAS check. Ask me about FAS, your Education Account, bills, payments, refunds, or say \"resume FAS check\" when you want to continue.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new(pt, "GENERAL", new(false, []), [], [], iv) { FollowUpQuestions = ["Resume FAS check.", "What is PCI?", "Show my Education Account balance."] };
        }

        if (AiKeywordMatchers.IsFasKnowledgeInterrupt(req.Message.ToUpperInvariant()))
        {
            st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused while answering a side question.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult(req.Message, "REDIRECT_KNOWLEDGE", new(false, []), [], [],
                FasConfirmationService.ToInterviewState(st, null))
            {
                FollowUpQuestions = ["Resume FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."]
            };
        }

        if (TryApplyFasCorrections(st, req.Message))
        {
            st.Status = "CONFIRMING"; var civ = FasConfirmationService.ToInterviewState(st, FasConfirmationService.ConfirmationPrompt(st));
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new($"I updated the FAS details.\n\n{FasConfirmationService.ConfirmationPrompt(st)}", "FAS_INTERVIEW", new(false, []), [], [], civ);
        }

        var cr = FasExtractionService.ExtractConfirmation(req.Message);
        if (cr.Status == "CLARIFY")
        {
            var civ = FasConfirmationService.ToInterviewState(st, FasConfirmationService.ConfirmationPrompt(st));
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new(cr.Message!, "FAS_INTERVIEW", new(false, []), [], [], civ);
        }

        if (cr.Value is bool cnf && !cnf)
        {
            st.Status = "CANCELLED"; st.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            var iv = FasConfirmationService.ToInterviewState(st, null);
            c.Touch("FAS_INTERVIEW", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult("No problem. I will not calculate eligibility from these answers. I stopped this FAS check; restart it if you want me to collect and confirm the details again.", "REDIRECT_FALLBACK", new(false, []), [], [], iv) { TurnIntent = "FAS_CONFIRMATION_REJECTED", FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."] };
        }

        st.Status = "COLLECTING_CONFIRMED";
        logger.LogInformation("FAS attestation: conversation {Id} confirmed all collected fields via CONFIRMING gate at {Time}. Snapshot: {Snapshot}", c.Id, now.ToString("O"), JsonSerializer.Serialize(st, JsonOptions));
        return null;
    }

    private async Task<AiHandlerResult> HandleCompletedFas(AiConversation c, AiChatRequest req, FasInterviewData st, string? pj, DateTime now, CancellationToken ct)
    {
        var schemes = st.RecommendationMatches.Count > 0 ? st.RecommendationMatches.ToArray() : st.IsWelfareHomeResident == true ? FasEligibilityService.WelfareHomeRecommendationMatches(st) : [];
        var iv = FasConfirmationService.ToInterviewState(st, null, schemes);
        bool asks = IsLiveSchemeEligibilityRequest(req.Message.ToUpperInvariant()) || AiKeywordMatchers.IsSchemeKbRequest(req.Message);
        string txt = asks && schemes.Length > 0 ? $"Your confirmed FAS check currently has {schemes.Length} eligible option{(schemes.Length == 1 ? "" : "s")}: {string.Join(", ", schemes.Select(x => x.SchemeName).Distinct(StringComparer.OrdinalIgnoreCase).Take(5))}. Use 'Apply answers to form' to copy the selected actionable schemes, then review the application before submitting."
            : st.IsWelfareHomeResident == true ? "You are marked as living in an approved welfare home. I prepared your confirmed details and open FAS scheme selection for the form. Use 'Apply answers to form', then review before submitting."
            : "I have confirmed the details for this FAS check. Use 'Apply answers to form' to copy them into the application, or edit the form manually if anything looks wrong.";
        List<AiAction> acts = [new("NAVIGATE", "Open FAS application", "/portal/fas", iv.FormPatch), new("APPLY_FAS_PATCH", "Apply answers to form", Payload: iv.FormPatch)];
        c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
        return new(txt, "GENERAL", new(false, []), [], acts, iv);
    }

    private async Task<AiHandlerResult> HandleStoppedFasTurn(AiConversation c, AiChatRequest req, FasInterviewData st, DateTime now, CancellationToken ct)
    {
        string pj = req.PageContext is null ? null! : JsonSerializer.Serialize(req.PageContext, JsonOptions);
        bool isCanc = st.Status == "CANCELLED";

        if (AiKeywordMatchers.LooksLikeCancelFas(req.Message))
        {
            st.Status = "CANCELLED"; st.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new("This FAS check is already stopped. I will not calculate eligibility from those answers unless you restart the check.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], FasConfirmationService.ToInterviewState(st, null)) { FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."] };
        }

        if (AiKeywordMatchers.LooksLikeScopeTest(req.Message))
        {
            if (!isCanc) { st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused before eligibility calculation."; }
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new("I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.", "GENERAL", new(false, []), [], [], FasConfirmationService.ToInterviewState(st, null)) { FollowUpQuestions = isCanc ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."] : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."] };
        }

        if (AiKeywordMatchers.IsFasKnowledgeInterrupt(req.Message.ToUpperInvariant()) || AiKeywordMatchers.LooksLikeCapabilityQuestion(req.Message) || AiKeywordMatchers.LooksLikeAdminCenterQuestion(req.Message))
        {
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult(req.Message, "REDIRECT_KNOWLEDGE", new(false, []), [], [],
                FasConfirmationService.ToInterviewState(st, null))
            {
                FollowUpQuestions = isCanc
                    ? FilterCurrentQuestion(["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], req.Message)
                    : FilterCurrentQuestion(["Resume FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], req.Message)
            };
        }

        if (AiKeywordMatchers.LooksLikePaymentQuery(req.Message.ToUpperInvariant()))
        {
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new AiHandlerResult(req.Message, "REDIRECT_PAYMENT", new(false, []), [], [],
                FasConfirmationService.ToInterviewState(st, null));
        }

        if (FasExtractionService.ExtractConfirmation(req.Message).Value is bool)
        {
            string txt = isCanc ? "That previous FAS check was stopped, so I will not treat this as confirmation. Say \"restart FAS check\" if you want to run a new eligibility check." : "The FAS check is paused. Say \"resume FAS check\" when you want to return to the confirmation step.";
            c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
            return new(txt, "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], FasConfirmationService.ToInterviewState(st, null)) { FollowUpQuestions = isCanc ? ["Restart FAS check.", "Open FAS application.", "What documents do I need for FAS?"] : ["Resume FAS check.", "Open FAS application.", "What documents do I need for FAS?"] };
        }

        c.Touch("GENERAL", pj, now); SaveFasState(c, st, now);
        return new(isCanc ? "This FAS check is stopped. Ask a student-finance question, open the FAS form, or say \"restart FAS check\" to begin again." : "This FAS check is paused. Ask a student-finance question, or say \"resume FAS check\" to continue.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], FasConfirmationService.ToInterviewState(st, null))
        { FollowUpQuestions = isCanc ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."] : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."] };
    }

    public static void ApplyFasTaskInterruptBeforeNonFasTurn(AiConversation c, string msg, string mode)
    {
        if (mode == "FAS_INTERVIEW" || c.FasSession is null) return;
        var st = LoadFasState(c);
        if (st is null || st.Status is "COMPLETE" or "CANCELLED") return;
        if (AiKeywordMatchers.LooksLikeCancelFas(msg)) { st.Status = "CANCELLED"; st.ValidationMessage = "FAS check stopped by user before eligibility calculation."; SaveFasState(c, st, DateTime.UtcNow); return; }
        if (AiKeywordMatchers.LooksLikeSwitchTopic(msg) || AiKeywordMatchers.LooksLikeScopeTest(msg) || mode == "PAYMENT") { st.Status = "PAUSED"; st.ValidationMessage = "FAS check paused before eligibility calculation."; SaveFasState(c, st, DateTime.UtcNow); }
    }

    private async Task<FasInterviewData> InitializeFasState(CancellationToken ct)
    {
        var profile = JsonSerializer.SerializeToElement(await fas.Prefill(ct), JsonOptions);
        var plan = await fas.EligibilityCriteriaPlan(ct);
        return new FasInterviewData { Profile = profile, Status = "COLLECTING", ApplicableSchemes = plan.ApplicableSchemes.Select(x => new FasApplicableSchemeOption(x.Id, x.Name)).ToList(), ApplicableSchemeNames = plan.ApplicableSchemeNames.ToList(), RequiredCriteriaTypes = plan.RequiredCriteriaTypes.ToList(), ProfileConfirmedFacts = plan.ProfileConfirmedFacts.ToList(), UserRequiredFacts = plan.UserRequiredFacts.ToList() };
    }

    private static string? NextQuestion(FasInterviewData s, string? preferred = null) => ResolveTargetField(s, preferred) switch
    {
        "isWelfareHomeResident" => "Are you currently residing in an approved welfare home? Please answer yes or no.",
        "monthlyHouseholdIncome" => "What is your total monthly household income in SGD?",
        "householdMemberCount" => "How many people are in your household?",
        "otherMonthlyIncome" => "Do you have any other monthly household income in SGD? Reply 0 if there is none.",
        "employmentStatusCode" => "What is your employment status? Choose employed, self-employed, or unemployed.",
        "email" => "What email address should we use for this FAS application?",
        "parentNationalities" => "Choose your parent or guardian's nationality: Singapore Citizen, Permanent Resident, or Foreigner.",
        _ => null
    };

    private static string? NextMissingField(FasInterviewData s, string? preferred = null)
    {
        if (preferred is not null)
        {
            if (preferred.Equals("isWelfareHomeResident", StringComparison.OrdinalIgnoreCase) && !s.IsWelfareHomeResident.HasValue) return "isWelfareHomeResident";
            if (preferred.Equals("email", StringComparison.OrdinalIgnoreCase) && s.Email is null) return "email";
            if (preferred.Equals("employmentStatusCode", StringComparison.OrdinalIgnoreCase) && s.IsWelfareHomeResident == false && s.EmploymentStatusCode is null) return "employmentStatusCode";
            if (preferred.Equals("monthlyHouseholdIncome", StringComparison.OrdinalIgnoreCase) && IncomeFactsRequired(s) && s.IsWelfareHomeResident == false && !s.MonthlyHouseholdIncome.HasValue) return "monthlyHouseholdIncome";
            if (preferred.Equals("householdMemberCount", StringComparison.OrdinalIgnoreCase) && IncomeFactsRequired(s) && s.IsWelfareHomeResident == false && (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0)) return "householdMemberCount";
            if (preferred.Equals("otherMonthlyIncome", StringComparison.OrdinalIgnoreCase) && IncomeFactsRequired(s) && s.IsWelfareHomeResident == false && !s.OtherMonthlyIncome.HasValue) return "otherMonthlyIncome";
            if (preferred.Equals("parentNationalities", StringComparison.OrdinalIgnoreCase) && ParentNationalityRequired(s) && s.ParentNationalities.Count == 0) return "parentNationalities";
        }
        if (!s.IsWelfareHomeResident.HasValue) return "isWelfareHomeResident";
        if (!s.IsWelfareHomeResident.Value && IncomeFactsRequired(s))
        {
            if (!s.MonthlyHouseholdIncome.HasValue) return "monthlyHouseholdIncome";
            if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0) return "householdMemberCount";
            if (!s.OtherMonthlyIncome.HasValue) return "otherMonthlyIncome";
        }
        if (ParentNationalityRequired(s) && s.ParentNationalities.Count == 0) return "parentNationalities";
        return null;
    }

    private static string? ResolveTargetField(FasInterviewData s, string? preferred = null) =>
        preferred is null ? s.ClarificationField ?? NextMissingField(s) : NextMissingField(s, preferred) ?? s.ClarificationField ?? NextMissingField(s);

    private static bool IsReadyForEligibilityComputation(FasInterviewData s) => s.Status == "COLLECTING_CONFIRMED";
    private static bool IncomeFactsRequired(FasInterviewData s) => CriteriaPlanUnknown(s) || s.RequiredCriteriaTypes.Any(c => c is "GDP" or "GHI" or "PCI") || s.ApplicableSchemes.Count > 0;
    private static bool ParentNationalityRequired(FasInterviewData _) => true;
    private static bool CriteriaPlanUnknown(FasInterviewData s) => s.RequiredCriteriaTypes.Count == 0 && s.ApplicableSchemeNames.Count == 0;
    internal static bool IsTerminalFasState(string status) => status is "CANCELLED" or "PAUSED" or "MANUAL_FALLBACK";

    internal static AiChatResponse AttachDormantFasState(AiChatResponse r, AiFasSession? s)
    {
        if (r.InterviewState is not null) return r;
        var st = s?.CollectedFactsJson is { Length: > 0 } j ? JsonSerializer.Deserialize<FasInterviewData>(j, JsonOptions) : null;
        return st is not null && IsTerminalFasState(st.Status) ? r with { InterviewState = FasConfirmationService.ToInterviewState(st, null) } : r;
    }

    private static bool TryApplyFasCorrections(FasInterviewData s, string msg)
    {
        if (!Regex.IsMatch(msg, @"\b(actually|change|correction|correct|wait|sorry|make that|meant|instead)\b", RegexOptions.IgnoreCase)) return false;
        bool ch = false; string lo = msg.ToLowerInvariant();
        var nums = FasExtractionService.ExtractNumbers(msg).ToArray();
        bool mem = Regex.IsMatch(lo, @"\b(member|members|people|pax|household size)\b");
        bool oth = Regex.IsMatch(lo, @"\b(other income|other monthly|additional income)\b");
        if (lo.Contains("welfare")) { var w = FasExtractionService.ExtractWelfareHome(msg); if (w.Status == "ACCEPTED") { FasExtractionService.ApplyAcceptedValue(s, "isWelfareHomeResident", w.Value); ch = true; } }
        if (Regex.IsMatch(lo, @"\b(income|salary|earn|household)\b") && !mem && !oth) { var i = FasExtractionService.ExtractIncome(msg); if (i.Status == "ACCEPTED") { FasExtractionService.ApplyAcceptedValue(s, "monthlyHouseholdIncome", i.Value); ch = true; } }
        else if (!mem && !oth && nums.Length == 1 && s.MonthlyHouseholdIncome.HasValue) { var v = nums[0]; if (v is >= 0 and <= 1_000_000) { FasExtractionService.ApplyAcceptedValue(s, "monthlyHouseholdIncome", decimal.Round(v, 2)); ch = true; } }
        if (mem) { var m = FasExtractionService.ExtractHouseholdMemberCount(msg); if (m.Status == "ACCEPTED") { FasExtractionService.ApplyAcceptedValue(s, "householdMemberCount", m.Value); ch = true; } }
        if (oth) { var o = FasExtractionService.ExtractOtherIncome(msg); if (o.Status == "ACCEPTED") { FasExtractionService.ApplyAcceptedValue(s, "otherMonthlyIncome", o.Value); ch = true; } }
        var nat = FasExtractionService.ExtractParentNationalities(msg);
        if (nat.Status != "ACCEPTED")
        {
            string? nn = FasExtractionService.TryNormalizeParentNationality(msg);
            if (nn is null && Regex.IsMatch(msg, @"\bPR\b", RegexOptions.IgnoreCase)) nn = "Permanent Resident";
            if (nn is null && Regex.IsMatch(msg, @"\bforeigner\b", RegexOptions.IgnoreCase)) nn = "Foreigner";
            if (nn is null && Regex.IsMatch(msg, @"\bsingapore(?: citizen|an)?\b", RegexOptions.IgnoreCase)) nn = "Singapore Citizen";
            if (nn is not null) nat = FasExtractionResult.Accepted(new[] { nn });
        }
        if (nat.Status == "ACCEPTED") { FasExtractionService.ApplyAcceptedValue(s, "parentNationalities", nat.Value); ch = true; }
        if (ch) { s.Status = "CONFIRMING"; s.ClarificationField = null; s.ValidationMessage = null; }
        return ch;
    }

    private static bool LooksLikeExplicitFasRestart(string m) =>
        Regex.IsMatch(m, @"\b(restart|start over|start again|resume|continue|go back|return|check|qualify|eligib)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(m, @"\b(fas|financial assistance|eligibility|check|application)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeContextualResume(string m) =>
        Regex.IsMatch(m, @"^\s*(resume|continue|resume please|continue please|i want to continue|keep going|go back)\s*[.!]?\s*$", RegexOptions.IgnoreCase);

    private static bool LooksLikeFasSchemeGuidanceRequest(string m) =>
        Regex.IsMatch(m, @"\b(scheme|schemes|recommend|recommendation|eligible|eligibility|qualify|apply for)\b", RegexOptions.IgnoreCase);

    private static bool IsLiveSchemeEligibilityRequest(string v) =>
        Regex.IsMatch(v, @"\b(WHICH|WHAT)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(v, @"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(v, @"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b", RegexOptions.IgnoreCase);

    private static string[] FilterCurrentQuestion(IEnumerable<string> fps, string msg)
    {
        string cq = Regex.Replace(msg.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant);
        return fps.Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !string.Equals(Regex.Replace(x.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant), cq, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }
}
