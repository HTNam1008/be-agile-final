using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FasInterviewHandler(
    StudentFasApplicationService fas,
    FallbackHandler fallback,
    PaymentQueryHandler paymentHandler,
    KnowledgeAnswerHandler knowledgeHandler,
    ILogger<FasInterviewHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static FasInterviewData? LoadFasState(AiConversation c) =>
        c.FasSession?.CollectedFactsJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions)
            : null;

    public static void SaveFasState(AiConversation c, FasInterviewData state, DateTime now)
    {
        c.FasSession ??= AiFasSession.Create(c.Id, now);
        c.FasSession.StatusCode = state.Status;
        c.FasSession.CollectedFactsJson = JsonSerializer.Serialize(state, JsonOptions);
        c.FasSession.UpdatedAtUtc = now;
    }

    public async Task<AiHandlerResult> HandleFasAsync(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        bool isNewInterview = c.FasSession is null;
        string? fieldKey = request.PageContext?.Entity is JsonElement entity &&
            entity.ValueKind == JsonValueKind.Object &&
            entity.TryGetProperty("fieldKey", out JsonElement fk) &&
            fk.ValueKind == JsonValueKind.String &&
            fk.GetString() is string fkStr
            ? fkStr
            : null;
        string? pageJson = request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        FasInterviewData state;
        try { state = LoadFasState(c) ?? await InitializeFasState(ct); }
        catch { return new AiHandlerResult("I couldn't read enough profile information from Singpass to help with FAS. You can still use the FAS form directly, or contact Admin Center for assistance.", "FALLBACK", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")]); }

        if (IsTerminalFasState(state.Status))
        {
            if (state.Status == "CANCELLED" && LooksLikeExplicitFasRestart(request.Message))
            {
                state = await InitializeFasState(ct);
                isNewInterview = true;
            }
            else if (LooksLikeExplicitFasRestart(request.Message) || LooksLikeContextualResume(request.Message))
            {
                state.Status = ResolveTargetField(state, fieldKey) is null ? "CONFIRMING" : "COLLECTING";
                state.ValidationMessage = null;
                state.ClarificationAttempts.Clear();
            }
            else
            {
                return await HandleStoppedFasTurn(c, request, state, now, ct);
            }
        }

        // CONFIRMING gate
        if (state.Status == "CONFIRMING")
        {
            AiHandlerResult? confirmingResult = await HandleConfirmingGate(c, request, state, now, ct);
            if (confirmingResult is not null)
                return confirmingResult;
        }

        // COMPLETE state after CONFIRMING moves to COLLECTING_CONFIRMED → eligibility computation
        if (!isNewInterview && state.Status == "COMPLETE")
        {
            return await HandleCompletedFas(c, request, state, pageJson, now, ct);
        }

        if (AiTurnRouter.LooksLikeCancelFas(request.Message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            AiInterviewState cancelledInterview = ToInterviewState(state, null);
            SaveFasState(c, state, now);
            return new AiHandlerResult("Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], cancelledInterview)
            {
                FollowUpQuestions = ["Show my outstanding course bills.", "Restart FAS check.", "What can you help me with?"]
            };
        }

        if (AiTurnRouter.LooksLikeSwitchTopic(request.Message) || AiTurnRouter.LooksLikeScopeTest(request.Message))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused before eligibility calculation.";
            AiInterviewState pausedInterview = ToInterviewState(state, null);
            string pausedText = AiTurnRouter.LooksLikeScopeTest(request.Message)
                ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(pausedText, "GENERAL", new(false, []), [], [], pausedInterview)
            {
                FollowUpQuestions = ["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"]
            };
        }

        // Side queries during active FAS interview
        if (AiTurnRouter.LooksLikePaymentQuery(request.Message.ToUpperInvariant()) || AiTurnRouter.LooksLikeCourseQuestion(request.Message))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused while answering a side question.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            AiHandlerResult sideResult = AiTurnRouter.LooksLikePaymentQuery(request.Message.ToUpperInvariant())
                ? await paymentHandler.HandlePaymentAsync(request, ct)
                : await knowledgeHandler.HandleGeneralAsync(c, request, ct);
            return sideResult with
            {
                InterviewState = ToInterviewState(state, null),
                FollowUpQuestions = FilterCurrentQuestion(["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"], request.Message)
            };
        }

        bool isGuidanceTurn = fieldKey is null && LooksLikeFasSchemeGuidanceRequest(request.Message);
        bool isFieldHelpTurn = fieldKey is not null && LooksLikeFieldHelpRequest(request.Message);
        bool shouldAskNextQuestion = (isNewInterview && fieldKey is null) || isGuidanceTurn || isFieldHelpTurn;
        string? answeredField = shouldAskNextQuestion ? null : ResolveTargetField(state, fieldKey);
        FasExtractionResult extraction = shouldAskNextQuestion ? FasExtractionResult.Accepted() : ApplyFasAnswer(state, request.Message, fieldKey);

        if (extraction.Status == "MANUAL_FALLBACK")
        {
            state.Status = "MANUAL_FALLBACK";
            AiInterviewState manualInterview = ToInterviewState(state, null);
            Guid review = await fallback.CreateReviewAsync(c, c.PersonId, "FAS_MANUAL_FALLBACK", request.PageContext, request.Message, now, ct);
            c.Touch("FAS_INTERVIEW", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(extraction.Message!, "FAS_INTERVIEW", new(false, []), [],
                [new("NAVIGATE", "Open FAS application", "/portal/fas"), new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })],
                manualInterview, review);
        }

        if (extraction.Status == "CLARIFY")
        {
            state.Status = "CLARIFYING";
            AiInterviewState clarificationInterview = ToInterviewState(state, extraction.Message);
            c.Touch("FAS_INTERVIEW", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(extraction.Message!, "FAS_INTERVIEW", new(false, []), [], [], clarificationInterview);
        }

        string? nextField = ResolveTargetField(state, fieldKey);
        string? next = NextQuestion(state, fieldKey);
        if (next is not null)
            state.ClarificationField = nextField;
        if (next is not null && state.Status == "COLLECTING_CONFIRMED")
            state.Status = "COLLECTING";

        object? recommendation = null;
        FasRecommendationMatch[] recommendedSchemes = [];
        string text;

        if (next is null && !IsReadyForEligibilityComputation(state))
        {
            state.Status = "CONFIRMING";
            text = ConfirmationPrompt(state);
        }
        else if (next is null && state.IsWelfareHomeResident == false)
        {
            (text, state, recommendedSchemes, recommendation) = await ComputeEligibility(state, ct);
        }
        else if (next is null)
        {
            state.Status = "COMPLETE";
            recommendedSchemes = WelfareHomeRecommendationMatches(state);
            state.RecommendationMatches = recommendedSchemes.ToList();
            text = recommendedSchemes.Length > 0
                ? $"I have your welfare-home status and parent or guardian nationality. I found {recommendedSchemes.Length} open FAS scheme{(recommendedSchemes.Length == 1 ? "" : "s")} for your school and prepared them for the form. Use 'Apply answers to form', then review before submitting."
                : "I have your welfare-home status and parent or guardian nationality. The FAS form will skip household income and household-size questions. I could not auto-select a scheme, so choose the scheme manually before submitting.";
        }
        else
        {
            state.Status = "COLLECTING";
            string? acknowledgement = extraction.Status == "ACCEPTED" ? AcceptedFieldAcknowledgement(answeredField, state) : null;
            text = isFieldHelpTurn
                ? next
                : shouldAskNextQuestion
                    ? $"{ProfileFactsIntro(state)}\n\n{next}"
                    : acknowledgement is null ? next : $"{acknowledgement}\n\n{next}";
        }

        AiInterviewState interview = ToInterviewState(state, next, recommendedSchemes);
        string endMode = state.Status == "COMPLETE" ? "GENERAL" : "FAS_INTERVIEW";
        c.Touch(endMode, pageJson, now); SaveFasState(c, state, now);
        List<AiCard> cards = recommendation is null ? [] : [new("FAS_RECOMMENDATION", recommendation)];
        Guid? fallbackReview = null;
        if (state.Status == "MANUAL_FALLBACK")
            fallbackReview = await fallback.CreateReviewAsync(c, c.PersonId, "FAS_MANUAL_FALLBACK", request.PageContext, request.Message, now, ct);
        List<AiAction> actions = state.Status == "COMPLETE"
            ? [new("NAVIGATE", "Open FAS application", "/portal/fas", interview.FormPatch)]
            : state.Status == "MANUAL_FALLBACK"
                ? [new("NAVIGATE", "Open FAS application", "/portal/fas")]
                : [];
        if (fallbackReview.HasValue) actions.Add(new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = fallbackReview.Value }));
        if (state.Status == "COMPLETE") actions.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: interview.FormPatch));
        string mode = state.Status == "COMPLETE" ? "GENERAL" : "FAS_INTERVIEW";
        return new AiHandlerResult(text, mode, new(false, []), cards, actions, interview, fallbackReview);
    }

    private async Task<AiHandlerResult?> HandleConfirmingGate(AiConversation c, AiChatRequest request, FasInterviewData state, DateTime now, CancellationToken ct)
    {
        string? pageJson = request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions);

        if (AiTurnRouter.LooksLikeCancelFas(request.Message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            AiInterviewState cancelledInterview = ToInterviewState(state, null);
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult("Got it. I stopped this FAS check and will not calculate eligibility from those answers. You can restart the FAS check later or open the form manually.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], cancelledInterview)
            {
                FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."]
            };
        }

        if (AiTurnRouter.LooksLikeScopeTest(request.Message) || AiTurnRouter.LooksLikeSwitchTopic(request.Message))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused before eligibility calculation.";
            AiInterviewState pausedInterview = ToInterviewState(state, null);
            string pausedText = AiTurnRouter.LooksLikeScopeTest(request.Message)
                ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                : "No problem. I paused this FAS check. Ask me about FAS, your Education Account, bills, payments, refunds, or say \"resume FAS check\" when you want to continue.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(pausedText, "GENERAL", new(false, []), [], [], pausedInterview)
            {
                FollowUpQuestions = ["Resume FAS check.", "What is PCI?", "Show my Education Account balance."]
            };
        }

        if (AiTurnRouter.IsFasKnowledgeInterrupt(request.Message.ToUpperInvariant()))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused while answering a side question.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            AiHandlerResult knowledgeResponse = await knowledgeHandler.HandleGeneralAsync(c, request, ct);
            return knowledgeResponse with
            {
                InterviewState = ToInterviewState(state, null),
                FollowUpQuestions = ["Resume FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."]
            };
        }

        if (TryApplyFasCorrections(state, request.Message))
        {
            state.Status = "CONFIRMING";
            AiInterviewState correctedInterview = ToInterviewState(state, ConfirmationPrompt(state));
            c.Touch("FAS_INTERVIEW", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult($"I updated the FAS details.\n\n{ConfirmationPrompt(state)}", "FAS_INTERVIEW", new(false, []), [], [], correctedInterview);
        }

        FasExtractionResult confirmation = ExtractConfirmation(request.Message);
        if (confirmation.Status == "CLARIFY")
        {
            AiInterviewState confirmInterview = ToInterviewState(state, ConfirmationPrompt(state));
            c.Touch("FAS_INTERVIEW", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(confirmation.Message!, "FAS_INTERVIEW", new(false, []), [], [], confirmInterview);
        }

        if (confirmation.Value is bool confirmed && !confirmed)
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            AiInterviewState manualInterview = ToInterviewState(state, null);
            Guid review = await fallback.CreateReviewAsync(c, c.PersonId, "FAS_CONFIRMATION_REJECTED", request.PageContext, request.Message, now, ct);
            c.Touch("FAS_INTERVIEW", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult("No problem. I will not calculate eligibility from these answers. I stopped this FAS check; restart it if you want me to collect and confirm the details again.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], manualInterview, review)
            {
                FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."]
            };
        }

        // Confirmed = true — move to COLLECTING_CONFIRMED to trigger eligibility
        state.Status = "COLLECTING_CONFIRMED";
        logger.LogInformation("FAS attestation: conversation {Id} confirmed all collected fields via CONFIRMING gate at {Time}. Snapshot: {Snapshot}",
            c.Id, now.ToString("O"), JsonSerializer.Serialize(state, JsonOptions));
        return null; // Signal to continue in HandleFasAsync main flow
    }

    private async Task<AiHandlerResult> HandleCompletedFas(AiConversation c, AiChatRequest request, FasInterviewData state, string? pageJson, DateTime now, CancellationToken ct)
    {
        FasRecommendationMatch[] completedSchemes = state.RecommendationMatches.Count > 0
            ? state.RecommendationMatches.ToArray()
            : state.IsWelfareHomeResident == true ? WelfareHomeRecommendationMatches(state) : [];
        AiInterviewState completedInterview = ToInterviewState(state, null, completedSchemes);
        bool asksForSchemes = IsLiveSchemeEligibilityRequest(request.Message.ToUpperInvariant()) || AiTurnRouter.IsSchemeKbRequest(request.Message);
        string completedText = asksForSchemes && completedSchemes.Length > 0
            ? $"Your confirmed FAS check currently has {completedSchemes.Length} eligible option{(completedSchemes.Length == 1 ? "" : "s")}: {string.Join(", ", completedSchemes.Select(x => x.SchemeName).Distinct(StringComparer.OrdinalIgnoreCase).Take(5))}. Use 'Apply answers to form' to copy the selected actionable schemes, then review the application before submitting."
            : state.IsWelfareHomeResident == true
            ? "You are marked as living in an approved welfare home. I prepared your confirmed details and open FAS scheme selection for the form. Use 'Apply answers to form', then review before submitting."
            : "I have confirmed the details for this FAS check. Use 'Apply answers to form' to copy them into the application, or edit the form manually if anything looks wrong.";
        List<AiAction> completedActions = [new("NAVIGATE", "Open FAS application", "/portal/fas", completedInterview.FormPatch), new("APPLY_FAS_PATCH", "Apply answers to form", Payload: completedInterview.FormPatch)];
        c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
        return new AiHandlerResult(completedText, "GENERAL", new(false, []), [], completedActions, completedInterview);
    }

    private async Task<AiHandlerResult> HandleStoppedFasTurn(AiConversation c, AiChatRequest request, FasInterviewData state, DateTime now, CancellationToken ct)
    {
        string pageJson = request.PageContext is null ? null! : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        bool isCancelled = state.Status == "CANCELLED";

        if (AiTurnRouter.LooksLikeCancelFas(request.Message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult("This FAS check is already stopped. I will not calculate eligibility from those answers unless you restart the check.", "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
            {
                FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."]
            };
        }

        if (AiTurnRouter.LooksLikeScopeTest(request.Message))
        {
            if (!isCancelled)
            {
                state.Status = "PAUSED";
                state.ValidationMessage = "FAS check paused before eligibility calculation.";
            }

            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult("I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.", "GENERAL", new(false, []), [], [], ToInterviewState(state, null))
            {
                FollowUpQuestions = isCancelled
                    ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."]
                    : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."]
            };
        }

        if (AiTurnRouter.IsFasKnowledgeInterrupt(request.Message.ToUpperInvariant()) ||
            AiTurnRouter.LooksLikeCapabilityQuestion(request.Message) ||
            AiTurnRouter.LooksLikeAdminCenterQuestion(request.Message))
        {
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            AiHandlerResult response = await knowledgeHandler.HandleGeneralAsync(c, request, ct);
            return response with
            {
                InterviewState = ToInterviewState(state, null),
                FollowUpQuestions = isCancelled
                    ? FilterCurrentQuestion(["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], request.Message)
                    : FilterCurrentQuestion(["Resume FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], request.Message)
            };
        }

        if (AiTurnRouter.LooksLikePaymentQuery(request.Message.ToUpperInvariant()))
        {
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            AiHandlerResult response = await paymentHandler.HandlePaymentAsync(request, ct);
            return response with { InterviewState = ToInterviewState(state, null) };
        }

        // Confirmation answers do not apply when FAS is stopped/paused
        if (ExtractConfirmation(request.Message).Value is bool)
        {
            string text = isCancelled
                ? "That previous FAS check was stopped, so I will not treat this as confirmation. Say \"restart FAS check\" if you want to run a new eligibility check."
                : "The FAS check is paused. Say \"resume FAS check\" when you want to return to the confirmation step.";
            c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
            return new AiHandlerResult(text, "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
            {
                FollowUpQuestions = isCancelled
                    ? ["Restart FAS check.", "Open FAS application.", "What documents do I need for FAS?"]
                    : ["Resume FAS check.", "Open FAS application.", "What documents do I need for FAS?"]
            };
        }

        c.Touch("GENERAL", pageJson, now); SaveFasState(c, state, now);
        return new AiHandlerResult(isCancelled
            ? "This FAS check is stopped. Ask a student-finance question, open the FAS form, or say \"restart FAS check\" to begin again."
            : "This FAS check is paused. Ask a student-finance question, or say \"resume FAS check\" to continue.",
            "GENERAL", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
        {
            FollowUpQuestions = isCancelled
                ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."]
                : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."]
        };
    }

    public static void ApplyFasTaskInterruptBeforeNonFasTurn(AiConversation conversation, string message, string mode)
    {
        if (mode == "FAS_INTERVIEW" || conversation.FasSession is null)
            return;

        FasInterviewData? state = LoadFasState(conversation);
        if (state is null || state.Status is "COMPLETE" or "CANCELLED")
            return;

        if (AiTurnRouter.LooksLikeCancelFas(message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            SaveFasState(conversation, state, DateTime.UtcNow);
            return;
        }

        if (AiTurnRouter.LooksLikeSwitchTopic(message) || AiTurnRouter.LooksLikeScopeTest(message) || mode == "PAYMENT")
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused before eligibility calculation.";
            SaveFasState(conversation, state, DateTime.UtcNow);
        }
    }

    // ── Eligibility Computation ─────────────────────────────────────────

    private async Task<(string Text, FasInterviewData State, FasRecommendationMatch[] Schemes, object? Recommendation)> ComputeEligibility(FasInterviewData state, CancellationToken ct)
    {
        try
        {
            object rawRecommendation = await fas.CheckEligibility(new EligibilityRequest(
                state.MonthlyHouseholdIncome ?? 0m,
                state.HouseholdMemberCount ?? 1,
                state.OtherMonthlyIncome ?? 0m,
                state.ParentNationalities), ct);
            JsonElement root = JsonSerializer.SerializeToElement(rawRecommendation, JsonOptions);
            bool hasSchemes = root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array && schemes.GetArrayLength() > 0;
            if (!hasSchemes && CanPrepareOpenSchemeForReview(state))
            {
                state.Status = "COMPLETE";
                FasRecommendationMatch[] recommendedSchemes = ReviewRequiredSchemeMatches(state);
                state.RecommendationMatches = recommendedSchemes.ToList();
                AiInterviewState completeInterview = ToInterviewState(state, null, recommendedSchemes);
                object recommendation = BuildReviewRequiredRecommendation(completeInterview, recommendedSchemes);
                return ($"I found {recommendedSchemes.Length} open FAS scheme{(recommendedSchemes.Length == 1 ? "" : "s")} for your school. The scheme criteria are not fully configured in the demo data, so I prepared your confirmed answers and scheme selection for review. Use 'Apply answers to form', then check the form before submitting.",
                    state, recommendedSchemes, recommendation);
            }
            if (!hasSchemes)
            {
                state.Status = "MANUAL_FALLBACK";
                return ("Based on your details, I could not find an eligible FAS scheme. Use 'Open FAS application' to review the form, or 'Contact Admin Center' below for staff help.",
                    state, [], null);
            }

            state.Status = "COMPLETE";
            FasRecommendationMatch[] matchedSchemes = ExtractRecommendationMatches(root);
            state.RecommendationMatches = matchedSchemes.ToList();
            AiInterviewState matchedInterview = ToInterviewState(state, null, matchedSchemes);
            object fasRecommendation = BuildFasRecommendation(root, matchedInterview);
            return ("I have enough information to evaluate the active FAS schemes. Review the recommendation below and use 'Apply answers to form' when ready.",
                state, matchedSchemes, fasRecommendation);
        }
        catch
        {
            if (CanPrepareOpenSchemeForReview(state))
            {
                state.Status = "COMPLETE";
                FasRecommendationMatch[] recommendedSchemes = ReviewRequiredSchemeMatches(state);
                state.RecommendationMatches = recommendedSchemes.ToList();
                AiInterviewState catchInterview = ToInterviewState(state, null, recommendedSchemes);
                object recommendation = BuildReviewRequiredRecommendation(catchInterview, recommendedSchemes);
                return ($"I found {recommendedSchemes.Length} open FAS scheme{(recommendedSchemes.Length == 1 ? "" : "s")} for your school. The scheme criteria are not fully configured in the demo data, so I prepared your confirmed answers and scheme selection for review. Use 'Apply answers to form', then check the form before submitting.",
                    state, recommendedSchemes, recommendation);
            }

            state.Status = "MANUAL_FALLBACK";
            return ("Based on your details, I could not find an eligible FAS scheme. Use 'Open FAS application' to review the form, or 'Contact Admin Center' below for staff help.",
                state, [], null);
        }
    }

    // ── State initialization ─────────────────────────────────────────────

    private async Task<FasInterviewData> InitializeFasState(CancellationToken ct)
    {
        JsonElement profile = JsonSerializer.SerializeToElement(await fas.Prefill(ct), JsonOptions);
        EligibilityCriteriaPlan criteriaPlan = await fas.EligibilityCriteriaPlan(ct);
        return new FasInterviewData
        {
            Profile = profile,
            Status = "COLLECTING",
            ApplicableSchemes = criteriaPlan.ApplicableSchemes.Select(x => new FasApplicableSchemeOption(x.Id, x.Name)).ToList(),
            ApplicableSchemeNames = criteriaPlan.ApplicableSchemeNames.ToList(),
            RequiredCriteriaTypes = criteriaPlan.RequiredCriteriaTypes.ToList(),
            ProfileConfirmedFacts = criteriaPlan.ProfileConfirmedFacts.ToList(),
            UserRequiredFacts = criteriaPlan.UserRequiredFacts.ToList()
        };
    }

    // ── FAS answer processing ────────────────────────────────────────────

    private static FasExtractionResult ApplyFasAnswer(FasInterviewData s, string message, string? preferredField = null)
    {
        string? field = ResolveTargetField(s, preferredField);
        if (field is null) return FasExtractionResult.Accepted();

        if (field == "parentNationalities" && !string.IsNullOrWhiteSpace(s.PendingParentNationalitySuggestion))
        {
            FasExtractionResult suggestionConfirmation = ExtractConfirmation(message);
            if (suggestionConfirmation.Value is bool acceptedSuggestion)
            {
                if (acceptedSuggestion)
                {
                    string suggestion = s.PendingParentNationalitySuggestion;
                    s.PendingParentNationalitySuggestion = null;
                    s.ClarificationField = null;
                    s.ValidationMessage = null;
                    s.ClarificationAttempts.Remove(field);
                    ApplyAcceptedValue(s, field, new[] { suggestion });
                    return FasExtractionResult.Accepted(new[] { suggestion });
                }

                s.PendingParentNationalitySuggestion = null;
                s.ClarificationField = field;
                s.ValidationMessage = ParentNationalityClarification();
                return FasExtractionResult.Clarify(ParentNationalityClarification());
            }
        }

        if (field != "isWelfareHomeResident" && LooksLikeWelfareHomeCorrection(message, s.IsWelfareHomeResident))
        {
            FasExtractionResult welfareCorrection = ExtractWelfareHome(message);
            if (welfareCorrection.Status == "ACCEPTED")
            {
                s.ClarificationField = null;
                s.ValidationMessage = null;
                s.ClarificationAttempts.Remove(field);
                s.ClarificationAttempts.Remove("isWelfareHomeResident");
                ApplyAcceptedValue(s, "isWelfareHomeResident", welfareCorrection.Value);
                return welfareCorrection;
            }
        }

        if (field != "email" && LooksLikeFieldHelpRequest(message))
        {
            if (LooksLikeUncertaintyAnswer(message))
            {
                int helpAttempts = s.ClarificationAttempts.GetValueOrDefault(field);
                if (helpAttempts >= 1)
                {
                    s.ClarificationField = null;
                    s.ValidationMessage = HelpForField(field);
                    return FasExtractionResult.ManualFallback("I couldn't safely prefill that field. The FAS form is still the source of truth; please complete it manually.");
                }

                s.ClarificationAttempts[field] = helpAttempts + 1;
            }

            s.ClarificationField = field;
            s.ValidationMessage = HelpForField(field);
            return FasExtractionResult.Clarify(HelpForField(field));
        }

        FasExtractionResult result = field switch
        {
            "isWelfareHomeResident" => ExtractWelfareHome(message),
            "monthlyHouseholdIncome" => ExtractIncome(message),
            "householdMemberCount" => ExtractHouseholdMemberCount(message),
            "otherMonthlyIncome" => ExtractOtherIncome(message),
            "employmentStatusCode" => ExtractEmploymentStatus(message),
            "email" => ExtractEmail(message),
            "parentNationalities" => ExtractParentNationalities(message),
            _ => FasExtractionResult.ManualFallback("I couldn't safely prefill that field. The FAS form is still the source of truth; please complete it manually.")
        };

        if (field == "parentNationalities" && result.Status == "CLARIFY" &&
            TryMapCountryToParentNationalitySuggestion(message) is string suggestedNationality)
        {
            s.PendingParentNationalitySuggestion = suggestedNationality;
            s.ClarificationField = field;
            s.ValidationMessage = $"{message.Trim().TrimEnd('?', '.', '!')} maps to {suggestedNationality} for this form. Should I record parent or guardian nationality as {suggestedNationality}?";
            return FasExtractionResult.Clarify(s.ValidationMessage);
        }

        if (result.Status == "ACCEPTED")
        {
            s.ClarificationField = null;
            s.ValidationMessage = null;
            s.PendingParentNationalitySuggestion = null;
            s.ClarificationAttempts.Remove(field);
            ApplyAcceptedValue(s, field, result.Value);
            return result;
        }

        int attempts = s.ClarificationAttempts.GetValueOrDefault(field);
        if (attempts >= 1)
        {
            s.ClarificationField = null;
            s.ValidationMessage = result.Message;
            return FasExtractionResult.ManualFallback("I couldn't safely prefill that field. The FAS form is still the source of truth; please complete it manually.");
        }

        s.ClarificationAttempts[field] = attempts + 1;
        s.ClarificationField = field;
        s.ValidationMessage = result.Message;
        return result;
    }

    // ── Extraction helpers ──────────────────────────────────────────────

    private static FasExtractionResult ExtractWelfareHome(string message)
    {
        string value = message.Trim().ToLowerInvariant();

        if (Regex.IsMatch(value, @"\b(not sure|maybe|i don't know|i do not know|what is|unsure|uncertain)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Clarify("Please confirm welfare-home status with yes or no.");

        bool notNegatesWelfare = Regex.IsMatch(value, @"\b(not|don't|do not)\b.{0,30}\b(welfare|approved|home|one)\b", RegexOptions.IgnoreCase);
        if (notNegatesWelfare) return FasExtractionResult.Accepted(false);

        bool yes = Regex.IsMatch(value, @"\b(yes|y|welfare home|approved welfare)\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(value, @"\b(do have|have|reside in|live in)\b.{0,30}\b(welfare|approved home|home|one)\b", RegexOptions.IgnoreCase);
        bool no = Regex.IsMatch(value, @"\b(no|n|not|do not|don't)\b", RegexOptions.IgnoreCase);

        if (yes && !no) return FasExtractionResult.Accepted(true);
        if (no && !yes) return FasExtractionResult.Accepted(false);
        return FasExtractionResult.Clarify("Please confirm welfare-home status with yes or no.");
    }

    private static FasExtractionResult ExtractConfirmation(string message)
    {
        string value = message.Trim();
        bool yes = Regex.IsMatch(value, @"^\s*(yes|y|correct|confirm|confirmed|looks right|that's right|that is right)\s*[.!]?\s*$", RegexOptions.IgnoreCase);
        bool no = Regex.IsMatch(value, @"^\s*(no|n|wrong|incorrect|edit|change|not correct|not right)\s*[.!]?\s*$", RegexOptions.IgnoreCase);
        if (yes && !no) return FasExtractionResult.Accepted(true);
        if (no && !yes) return FasExtractionResult.Accepted(false);
        return FasExtractionResult.Clarify("Please reply yes if these details are correct, or no if you want to stop and edit the form manually.");
    }

    private static FasExtractionResult ExtractIncome(string message)
    {
        decimal[] numbers = ExtractNumbers(message).ToArray();
        if (numbers.Length == 0) return FasExtractionResult.Clarify("Please provide your total monthly household income as an SGD amount, for example 3200.");
        if (numbers.Length > 1) return FasExtractionResult.Clarify("I found more than one amount. Please reply with only the total monthly household income in SGD.");
        decimal income = numbers[0];
        if (income < 0 || income > 1_000_000) return FasExtractionResult.Clarify("Please provide a valid non-negative monthly household income in SGD.");
        return FasExtractionResult.Accepted(decimal.Round(income, 2));
    }

    private static FasExtractionResult ExtractHouseholdMemberCount(string message)
    {
        decimal[] numbers = ExtractNumbers(message).ToArray();
        if (numbers.Length == 0) return FasExtractionResult.Clarify("Please provide the number of people in your household, for example 4.");
        if (numbers.Length > 1 || numbers[0] != decimal.Truncate(numbers[0])) return FasExtractionResult.Clarify("Please reply with one whole number for household members.");
        int count = (int)numbers[0];
        if (count is < 1 or > 30) return FasExtractionResult.Clarify("Please provide a household member count between 1 and 30.");
        return FasExtractionResult.Accepted(count);
    }

    private static FasExtractionResult ExtractOtherIncome(string message)
    {
        if (Regex.IsMatch(message, @"\b(none|no other|nothing|nil|zero)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Accepted(0m);
        FasExtractionResult result = ExtractIncome(message);
        return result.Status == "CLARIFY" && result.Message is not null
            ? FasExtractionResult.Clarify(result.Message.Replace("total monthly household income", "other monthly household income", StringComparison.OrdinalIgnoreCase))
            : result;
    }

    private static FasExtractionResult ExtractEmploymentStatus(string message)
    {
        string value = message.Trim().ToLowerInvariant();
        if (Regex.IsMatch(value, @"\b(self[-\s]?employed|freelance|freelancer)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Accepted("SELF_EMPLOYED");
        if (Regex.IsMatch(value, @"\b(unemployed|not employed|no job|jobless)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Accepted("UNEMPLOYED");
        if (Regex.IsMatch(value, @"\b(employed|working|employee|full[-\s]?time|part[-\s]?time)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Accepted("EMPLOYED");
        return FasExtractionResult.Clarify("Please choose employed, self-employed, or unemployed.");
    }

    private static FasExtractionResult ExtractEmail(string message)
    {
        Match match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? FasExtractionResult.Accepted(match.Value)
            : FasExtractionResult.Clarify("Please provide a valid email address, for example student@example.com.");
    }

    private static FasExtractionResult ExtractParentNationalities(string message)
    {
        string normalized = message.Trim();
        if (normalized.Length is < 2 or > 120 || ExtractNumbers(normalized).Any())
            return FasExtractionResult.Clarify(ParentNationalityClarification());

        string[] values = Regex.Split(normalized, @"\s*(?:,|/|\band\b|&)\s*", RegexOptions.IgnoreCase)
            .Select(x => TryNormalizeParentNationality(x))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int requestedValues = Regex.Split(normalized, @"\s*(?:,|/|\band\b|&)\s*", RegexOptions.IgnoreCase).Count(x => !string.IsNullOrWhiteSpace(x));
        return values.Length == 0 || values.Length != requestedValues
            ? FasExtractionResult.Clarify(ParentNationalityClarification())
            : FasExtractionResult.Accepted(values);
    }

    private static IEnumerable<decimal> ExtractNumbers(string message)
    {
        foreach (Match match in Regex.Matches(message, @"(?<![\w])-?\d[\d,]*(?:\.\d+)?\s*[kK]?", RegexOptions.CultureInvariant))
        {
            string raw = match.Value.Trim();
            bool thousand = raw.EndsWith("k", StringComparison.OrdinalIgnoreCase);
            raw = raw.TrimEnd('k', 'K').Replace(",", string.Empty);
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                yield return thousand ? value * 1000 : value;
        }
    }

    private static string? TryNormalizeParentNationality(string value)
    {
        string trimmed = Regex.Replace(value.Trim().Trim('.'), @"\s+", " ");
        string compact = Regex.Replace(trimmed, @"[\s._-]+", "", RegexOptions.CultureInvariant).ToUpperInvariant();
        if (compact is "SG" or "SINGAPORE" or "SINGAPOREAN" or "SINGAPORECITIZEN" or "CITIZEN")
            return "Singapore Citizen";
        if (compact is "PR" or "PERMANENTRESIDENT" or "SINGAPOREPR")
            return "Permanent Resident";
        if (compact is "FOREIGNER" or "FOREIGN" or "INTERNATIONAL" or "INTERNATIONALSTUDENT" or "NONCITIZEN" or "NONRESIDENT")
            return "Foreigner";
        return null;
    }

    private static string? TryMapCountryToParentNationalitySuggestion(string value)
    {
        string normalized = Regex.Replace(value.Trim().Trim('?', '.', '!', ','), @"\s+", " ");
        string compact = Regex.Replace(normalized, @"[^A-Za-z]", "", RegexOptions.CultureInvariant).ToUpperInvariant();
        if (compact is "SINGAPORE" or "SG")
            return "Singapore Citizen";

        if (compact is
            "VIETNAM" or "VIETNAMESE" or "MALAYSIA" or "MALAYSIAN" or "INDIA" or "INDIAN" or
            "CHINA" or "CHINESE" or "INDONESIA" or "INDONESIAN" or "PHILIPPINES" or "FILIPINO" or
            "THAILAND" or "THAI" or "MYANMAR" or "BURMESE" or "CAMBODIA" or "CAMBODIAN" or
            "LAOS" or "LAOTIAN" or "JAPAN" or "JAPANESE" or "KOREA" or "KOREAN")
            return "Foreigner";

        return null;
    }

    private static string ParentNationalityClarification() =>
        "Choose one of these parent or guardian nationality options: Singapore Citizen, Permanent Resident, or Foreigner.";

    // ── Interview field Q&A ─────────────────────────────────────────────

    private static string? NextQuestion(FasInterviewData s, string? preferred = null)
    {
        string? field = ResolveTargetField(s, preferred);
        return field switch
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
    }

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

    private static string? ResolveTargetField(FasInterviewData s, string? preferred = null)
    {
        return preferred is null
            ? s.ClarificationField ?? NextMissingField(s)
            : NextMissingField(s, preferred) ?? s.ClarificationField ?? NextMissingField(s);
    }

    private static bool IsReadyForEligibilityComputation(FasInterviewData s) => s.Status == "COLLECTING_CONFIRMED";

    private static bool IncomeFactsRequired(FasInterviewData s) =>
        CriteriaPlanUnknown(s) || s.RequiredCriteriaTypes.Any(IsIncomeCriterion) || s.ApplicableSchemes.Count > 0;

    private static bool ParentNationalityRequired(FasInterviewData _) => true;

    private static bool CriteriaPlanUnknown(FasInterviewData s) => s.RequiredCriteriaTypes.Count == 0 && s.ApplicableSchemeNames.Count == 0;

    private static bool IsIncomeCriterion(string criteriaType) => criteriaType is "GDP" or "GHI" or "PCI";

    internal static bool IsTerminalFasState(string status) =>
        status is "CANCELLED" or "PAUSED" or "MANUAL_FALLBACK";

    internal static AiChatResponse AttachDormantFasState(AiChatResponse response, AiFasSession? session)
    {
        if (response.InterviewState is not null)
            return response;

        FasInterviewData? state = session?.CollectedFactsJson is { Length: > 0 } json
            ? JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions)
            : null;
        return state is not null && IsTerminalFasState(state.Status)
            ? response with { InterviewState = ToInterviewState(state, null) }
            : response;
    }

    // ── Confirmation prompt ─────────────────────────────────────────────

    private static string ConfirmationPrompt(FasInterviewData s)
    {
        List<string> facts = [];
        string welfareDisplay = s.IsWelfareHomeResident.HasValue
            ? (s.IsWelfareHomeResident.Value ? "Yes" : "No")
            : "Not answered";
        facts.Add($"Welfare home: {welfareDisplay}");
        if (s.IsWelfareHomeResident == false)
        {
            facts.Add($"Monthly household income: {s.MonthlyHouseholdIncome?.ToString("C", CultureInfo.GetCultureInfo("en-SG")) ?? "Not provided"}");
            facts.Add($"Household members: {s.HouseholdMemberCount?.ToString(CultureInfo.InvariantCulture) ?? "Not provided"}");
            facts.Add($"Other monthly income: {s.OtherMonthlyIncome?.ToString("C", CultureInfo.GetCultureInfo("en-SG")) ?? "Not provided"}");
        }
        facts.Add($"Parent or guardian nationality: {(s.ParentNationalities.Count == 0 ? "Not provided" : string.Join(", ", s.ParentNationalities))}");

        return $"Before I calculate FAS eligibility, please confirm these details are correct:\n\n{string.Join("\n", facts.Select(x => $"- {x}"))}\n\nReply yes to calculate eligibility, or no to stop and edit the form manually.";
    }

    private static string ProfileFactsIntro(FasInterviewData s)
    {
        List<string> facts = s.ProfileConfirmedFacts.Count > 0 ? s.ProfileConfirmedFacts : [];
        if (facts.Count == 0)
        {
            string? nationality = TryGetString(s.Profile, "nationalityCode");
            string? accountType = TryGetString(s.Profile, "accountTypeCode");
            string? school = TryGetString(s.Profile, "schoolName");
            string? dateOfBirth = TryGetString(s.Profile, "dateOfBirth");
            if (!string.IsNullOrWhiteSpace(school)) facts.Add($"school: {school}");
            if (!string.IsNullOrWhiteSpace(nationality)) facts.Add($"student nationality: {nationality}");
            if (!string.IsNullOrWhiteSpace(accountType)) facts.Add($"account type: {accountType}");
            if (!string.IsNullOrWhiteSpace(dateOfBirth)) facts.Add("date of birth");
        }

        string schemeText = s.ApplicableSchemeNames.Count switch
        {
            0 when !CriteriaPlanUnknown(s) => "I did not find an open FAS scheme for your school yet.",
            0 => "I will check the active FAS schemes for your school.",
            1 => $"I found 1 open FAS scheme for your school: {s.ApplicableSchemeNames[0]}.",
            _ => $"I found {s.ApplicableSchemeNames.Count} open FAS schemes for your school: {string.Join(", ", s.ApplicableSchemeNames.Take(3))}{(s.ApplicableSchemeNames.Count > 3 ? ", and more" : string.Empty)}."
        };

        string factText = facts.Count == 0
            ? "I will only ask for details missing from your FAS eligibility check."
            : $"I already have these MOE record facts: {string.Join(", ", facts)}.";
        string askText = s.UserRequiredFacts.Count == 0
            ? "I will ask only what is still needed."
            : $"I still need: {string.Join(", ", s.UserRequiredFacts)}.";

        return $"{schemeText} {factText} {askText}";
    }

    private static string? AcceptedFieldAcknowledgement(string? field, FasInterviewData s) => field switch
    {
        "isWelfareHomeResident" when s.IsWelfareHomeResident.HasValue => s.IsWelfareHomeResident.Value
            ? "Got it - I recorded that you are residing in an approved welfare home."
            : "Got it - I recorded that you are not residing in an approved welfare home.",
        "monthlyHouseholdIncome" when s.MonthlyHouseholdIncome.HasValue => $"Got it - I recorded monthly household income as {s.MonthlyHouseholdIncome.Value.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.",
        "householdMemberCount" when s.HouseholdMemberCount.HasValue => $"Got it - I recorded {s.HouseholdMemberCount.Value} household member{(s.HouseholdMemberCount.Value == 1 ? "" : "s")}.",
        "otherMonthlyIncome" when s.OtherMonthlyIncome.HasValue => $"Got it - I recorded other monthly household income as {s.OtherMonthlyIncome.Value.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.",
        "employmentStatusCode" when s.EmploymentStatusCode is not null => $"Got it - I recorded employment status as {HumanizeEmploymentStatus(s.EmploymentStatusCode)}.",
        "email" when s.Email is not null => $"Got it - I recorded {s.Email} as the application email.",
        "parentNationalities" when s.ParentNationalities.Count > 0 => $"Got it - I recorded parent or guardian nationality as {string.Join(", ", s.ParentNationalities)}.",
        _ => null
    };

    private static string HumanizeEmploymentStatus(string value) => value switch
    {
        "SELF_EMPLOYED" => "self-employed",
        "UNEMPLOYED" => "unemployed",
        "EMPLOYED" => "employed",
        _ => value
    };

    // ── Interview state ────────────────────────────────────────────────

    private static AiInterviewState ToInterviewState(FasInterviewData s, string? next, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        List<AiInterviewField> fields =
        [
            new("isWelfareHomeResident", s.IsWelfareHomeResident, s.IsWelfareHomeResident.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.IsWelfareHomeResident.HasValue),
            new("email", s.Email ?? TryGetString(s.Profile, "email"), s.Email is not null ? "AI_CONFIRMED" : TryGetString(s.Profile, "email") is not null ? "PROFILE" : "UNMAPPED", s.Email is not null || TryGetString(s.Profile, "email") is not null),
            new("employmentStatusCode", s.EmploymentStatusCode ?? TryGetString(s.Profile, "employmentStatusCode"), s.EmploymentStatusCode is not null ? "AI_CONFIRMED" : TryGetString(s.Profile, "employmentStatusCode") is not null ? "PROFILE" : "UNMAPPED", s.EmploymentStatusCode is not null || TryGetString(s.Profile, "employmentStatusCode") is not null),
            new("monthlyHouseholdIncome", s.MonthlyHouseholdIncome, s.MonthlyHouseholdIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.MonthlyHouseholdIncome.HasValue),
            new("householdMemberCount", s.HouseholdMemberCount, s.HouseholdMemberCount.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.HouseholdMemberCount.HasValue),
            new("otherMonthlyIncome", s.OtherMonthlyIncome, s.OtherMonthlyIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.OtherMonthlyIncome.HasValue),
            new("parentNationalities", s.ParentNationalities, s.ParentNationalities.Count > 0 ? "AI_CONFIRMED" : "UNMAPPED", s.ParentNationalities.Count > 0)
        ];
        string[] missing = fields.Where(x => FieldCountsAsMissing(s, x.Name, x.Confirmed))
            .Select(x => x.Name).ToArray();
        object? patch = s.Status == "MANUAL_FALLBACK" ? null : BuildFasFormPatch(s, recommendedSchemes);
        return new(s.Status, next, fields, missing, patch);
    }

    private static FasFormPatch BuildFasFormPatch(FasInterviewData s, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        var particulars = new FasPatchParticulars(
            Email: s.Email ?? TryGetString(s.Profile, "email"),
            ParentNationalities: s.ParentNationalities.Count > 0 ? s.ParentNationalities.ToArray() : null);
        var income = new FasPatchIncome(
            s.IsWelfareHomeResident,
            s.EmploymentStatusCode ?? TryGetString(s.Profile, "employmentStatusCode") ?? "EMPLOYED",
            s.MonthlyHouseholdIncome,
            s.HouseholdMemberCount,
            s.OtherMonthlyIncome);
        var meta = new Dictionary<string, FasPatchMetaField>();
        void AddMeta(string name, object? value, string provenance, string? explanation = null)
        {
            string conf = value switch
            {
                null => "LOW",
                _ when s.ClarificationAttempts.GetValueOrDefault(name) >= 1 => "MEDIUM",
                _ => "HIGH"
            };
            meta[name] = new FasPatchMetaField(conf, provenance, explanation);
        }
        bool skipIncome = s.IsWelfareHomeResident == true;
        AddMeta("isWelfareHomeResident", s.IsWelfareHomeResident, s.IsWelfareHomeResident.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
            s.IsWelfareHomeResident.HasValue ? (s.IsWelfareHomeResident.Value ? "Confirmed welfare home resident." : "Confirmed not a welfare home resident.") : null);
        if (!skipIncome)
        {
            AddMeta("email", s.Email ?? TryGetString(s.Profile, "email"), s.Email is null ? "PROFILE" : "AI_CONFIRMED", s.Email is null ? "From your profile." : "Confirmed in chat.");
            AddMeta("employmentStatusCode", s.EmploymentStatusCode ?? TryGetString(s.Profile, "employmentStatusCode"), s.EmploymentStatusCode is null ? "PROFILE" : "AI_CONFIRMED", s.EmploymentStatusCode is null ? "From your profile." : "Confirmed in chat.");
            AddMeta("monthlyHouseholdIncome", s.MonthlyHouseholdIncome, s.MonthlyHouseholdIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
                s.MonthlyHouseholdIncome.HasValue ? $"You said your household income is ${s.MonthlyHouseholdIncome.Value:N0}." : null);
            AddMeta("householdMemberCount", s.HouseholdMemberCount, s.HouseholdMemberCount.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
                s.HouseholdMemberCount.HasValue ? $"You said there are {s.HouseholdMemberCount.Value} household members." : null);
            AddMeta("otherMonthlyIncome", s.OtherMonthlyIncome, s.OtherMonthlyIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
                s.OtherMonthlyIncome.HasValue ? $"You said other monthly income is ${s.OtherMonthlyIncome.Value:N0}." : null);
        }
        AddMeta("parentNationalities", s.ParentNationalities.Count > 0 ? s.ParentNationalities : null,
            s.ParentNationalities.Count > 0 ? "AI_CONFIRMED" : "UNMAPPED",
            s.ParentNationalities.Count > 0 ? $"Nationalit{(s.ParentNationalities.Count == 1 ? "y" : "ies")}: {string.Join(", ", s.ParentNationalities)}." : null);
        FasRecommendationMatch[] actionableSchemes = recommendedSchemes?
            .Where(x => x.CanApply && !x.HasPendingApplication)
            .GroupBy(x => x.SchemeId)
            .Select(x => x.First())
            .ToArray() ?? [];
        FasPatchSchemes? schemes = actionableSchemes.Length > 0
            ? new FasPatchSchemes(actionableSchemes.Select(x => x.SchemeId).ToArray(),
                actionableSchemes.Select(x => x.SchemeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            : null;
        AddMeta("schemeIds", schemes?.RecommendedSchemeIds, schemes is null ? "UNMAPPED" : "AI_CONFIRMED",
            schemes is null ? null : "Recommended from open schemes for your school.");
        return new FasFormPatch(particulars, income, schemes, meta);
    }

    private static bool FieldCountsAsMissing(FasInterviewData s, string fieldName, bool confirmed)
    {
        if (confirmed) return false;
        if (s.IsWelfareHomeResident == true && fieldName is "monthlyHouseholdIncome" or "householdMemberCount" or "otherMonthlyIncome") return false;
        if (fieldName is "monthlyHouseholdIncome" or "householdMemberCount" or "otherMonthlyIncome") return IncomeFactsRequired(s);
        if (fieldName == "parentNationalities") return ParentNationalityRequired(s);
        if (fieldName is "email" or "employmentStatusCode") return false;
        return true;
    }

    private static void ApplyAcceptedValue(FasInterviewData s, string field, object? value)
    {
        switch (field)
        {
            case "isWelfareHomeResident":
                s.IsWelfareHomeResident = (bool)value!;
                if (s.IsWelfareHomeResident.Value)
                {
                    s.MonthlyHouseholdIncome = null;
                    s.HouseholdMemberCount = null;
                    s.OtherMonthlyIncome = null;
                }
                break;
            case "email":
                s.Email = (string)value!;
                break;
            case "employmentStatusCode":
                s.EmploymentStatusCode = (string)value!;
                break;
            case "monthlyHouseholdIncome":
                s.MonthlyHouseholdIncome = (decimal)value!;
                break;
            case "householdMemberCount":
                s.HouseholdMemberCount = (int)value!;
                break;
            case "otherMonthlyIncome":
                s.OtherMonthlyIncome = (decimal)value!;
                break;
            case "parentNationalities":
                s.ParentNationalities = ((IReadOnlyCollection<string>)value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                break;
        }
    }

    // ── Correction handling ─────────────────────────────────────────────

    private static bool TryApplyFasCorrections(FasInterviewData s, string message)
    {
        if (!Regex.IsMatch(message, @"\b(actually|change|correction|correct|wait|sorry|make that|meant|instead)\b", RegexOptions.IgnoreCase))
            return false;

        bool changed = false;
        string lower = message.ToLowerInvariant();
        decimal[] numbers = ExtractNumbers(message).ToArray();
        bool mentionsMembers = Regex.IsMatch(lower, @"\b(member|members|people|pax|household size)\b");
        bool mentionsOtherIncome = Regex.IsMatch(lower, @"\b(other income|other monthly|additional income)\b");
        if (lower.Contains("welfare"))
        {
            FasExtractionResult welfare = ExtractWelfareHome(message);
            if (welfare.Status == "ACCEPTED")
            {
                ApplyAcceptedValue(s, "isWelfareHomeResident", welfare.Value);
                changed = true;
            }
        }

        if (Regex.IsMatch(lower, @"\b(income|salary|earn|household)\b") && !mentionsMembers && !mentionsOtherIncome)
        {
            FasExtractionResult income = ExtractIncome(message);
            if (income.Status == "ACCEPTED")
            {
                ApplyAcceptedValue(s, "monthlyHouseholdIncome", income.Value);
                changed = true;
            }
        }
        else if (!mentionsMembers && !mentionsOtherIncome && numbers.Length == 1 && s.MonthlyHouseholdIncome.HasValue)
        {
            decimal value = numbers[0];
            if (value is >= 0 and <= 1_000_000)
            {
                ApplyAcceptedValue(s, "monthlyHouseholdIncome", decimal.Round(value, 2));
                changed = true;
            }
        }

        if (mentionsMembers)
        {
            FasExtractionResult members = ExtractHouseholdMemberCount(message);
            if (members.Status == "ACCEPTED")
            {
                ApplyAcceptedValue(s, "householdMemberCount", members.Value);
                changed = true;
            }
        }

        if (mentionsOtherIncome)
        {
            FasExtractionResult other = ExtractOtherIncome(message);
            if (other.Status == "ACCEPTED")
            {
                ApplyAcceptedValue(s, "otherMonthlyIncome", other.Value);
                changed = true;
            }
        }

        FasExtractionResult nationality = ExtractParentNationalities(message);
        if (nationality.Status != "ACCEPTED")
        {
            string? normalizedNationality = TryNormalizeParentNationality(message);
            if (normalizedNationality is null && Regex.IsMatch(message, @"\bPR\b", RegexOptions.IgnoreCase))
                normalizedNationality = "Permanent Resident";
            if (normalizedNationality is null && Regex.IsMatch(message, @"\bforeigner\b", RegexOptions.IgnoreCase))
                normalizedNationality = "Foreigner";
            if (normalizedNationality is null && Regex.IsMatch(message, @"\bsingapore(?: citizen|an)?\b", RegexOptions.IgnoreCase))
                normalizedNationality = "Singapore Citizen";
            if (normalizedNationality is not null)
                nationality = FasExtractionResult.Accepted(new[] { normalizedNationality });
        }
        if (nationality.Status == "ACCEPTED")
        {
            ApplyAcceptedValue(s, "parentNationalities", nationality.Value);
            changed = true;
        }

        if (changed)
        {
            s.Status = "CONFIRMING";
            s.ClarificationField = null;
            s.ValidationMessage = null;
        }
        return changed;
    }

    // ── Recommendation building ─────────────────────────────────────────

    private static FasRecommendationCard BuildFasRecommendation(object rawRecommendation, AiInterviewState interview)
    {
        JsonElement root = JsonSerializer.SerializeToElement(rawRecommendation, JsonOptions);
        decimal? pci = TryGetDecimal(root, "perCapitaIncome");
        FasRecommendationMatch[] matches = ExtractRecommendationMatches(root);
        FasRecommendationMatch? recommended = matches.FirstOrDefault();
        bool isComparable = matches.Length == 0 || matches.All(x => x.IsComparable);
        bool allFieldsConfirmed = interview.Fields.All(f => f.Confirmed);
        string confidence = allFieldsConfirmed && isComparable ? "HIGH" : "REVIEW_REQUIRED";
        bool hasPendingHigherBenefit = recommended is not null &&
            matches.Any(x => x.HasPendingApplication && BenefitRank(x) > BenefitRank(recommended));
        return new FasRecommendationCard(
            pci,
            recommended?.SchemeName,
            recommended?.TierLabel,
            recommended?.SubsidyType,
            recommended?.SubsidyValue,
            matches,
            interview.Fields.Where(x => x.Confirmed).ToArray(),
            interview.MissingFields,
            "Prototype recommendation. Eligibility is calculated by application code and final approval remains subject to MOE review.",
            confidence,
            isComparable,
            hasPendingHigherBenefit
                ? "Ranked by schemes you can apply for now first, then comparable benefit strength, application closing date, and scheme name. Matched schemes with pending applications are shown after schemes you can apply for now."
                : isComparable
                    ? "Ranked by schemes you can apply for now first, then comparable benefit strength, application closing date, and scheme name."
                    : "Eligible schemes use benefit types that are not directly comparable without a course fee amount; schemes you can apply for now are shown first.");
    }

    private static decimal BenefitRank(FasRecommendationMatch match)
    {
        string subsidyType = match.SubsidyType.ToUpperInvariant();
        return subsidyType switch
        {
            "PERCENTAGE" => match.SubsidyValue * 1000m,
            "FIXED" => match.SubsidyValue,
            _ => 0m
        };
    }

    private static bool CanPrepareOpenSchemeForReview(FasInterviewData state) =>
        state.ApplicableSchemes.Count > 0 && state.RequiredCriteriaTypes.Count == 0;

    private static FasRecommendationMatch[] ReviewRequiredSchemeMatches(FasInterviewData state) =>
        state.ApplicableSchemes
            .Select((scheme, index) => new FasRecommendationMatch(scheme.Id, scheme.Name, 0, "Review required", "Scheme selection", 0m,
                index + 1, "Open scheme for your school. Criteria are not fully configured for automatic ranking, so staff/form review is required.", "REVIEW_REQUIRED", false))
            .ToArray();

    private static FasRecommendationCard BuildReviewRequiredRecommendation(AiInterviewState interview, IReadOnlyCollection<FasRecommendationMatch> matches)
    {
        FasRecommendationMatch? recommended = matches.FirstOrDefault();
        return new FasRecommendationCard(
            null,
            recommended?.SchemeName,
            recommended?.TierLabel,
            recommended?.SubsidyType,
            recommended?.SubsidyValue,
            matches,
            interview.Fields.Where(x => x.Confirmed).ToArray(),
            interview.MissingFields,
            "Review required. The scheme is open for your school, but the demo criteria do not include a configured tier calculation.",
            "REVIEW_REQUIRED",
            false,
            "Open schemes without configured tier criteria are shown for review, not ranked as a best fit.");
    }

    private static FasRecommendationMatch[] ExtractRecommendationMatches(JsonElement root)
    {
        return root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array
            ? schemes.EnumerateArray().Select(ToRecommendationMatch).Where(x => x is not null).Cast<FasRecommendationMatch>().ToArray()
            : [];
    }

    private static FasRecommendationMatch? ToRecommendationMatch(JsonElement item)
    {
        long? schemeId = TryGetInt64(item, "schemeId");
        long? tierId = TryGetInt64(item, "tierId");
        string? schemeName = TryGetString(item, "schemeName");
        string? tierLabel = TryGetString(item, "tierLabel");
        string? subsidyType = TryGetString(item, "subsidyType");
        decimal? subsidyValue = TryGetDecimal(item, "subsidyValue");
        int? recommendationRank = TryGetInt32(item, "recommendationRank");
        string? recommendationReason = TryGetString(item, "recommendationReason");
        string? recommendationConfidence = TryGetString(item, "recommendationConfidence");
        bool? isComparable = TryGetBoolean(item, "isComparable");
        bool? canApply = TryGetBoolean(item, "canApply");
        bool? hasPendingApplication = TryGetBoolean(item, "hasPendingApplication");
        long? pendingApplicationId = TryGetInt64(item, "pendingApplicationId");
        return schemeId.HasValue && tierId.HasValue && schemeName is not null && tierLabel is not null && subsidyType is not null && subsidyValue.HasValue
            ? new FasRecommendationMatch(schemeId.Value, schemeName, tierId.Value, tierLabel, subsidyType, subsidyValue.Value,
                recommendationRank ?? 0, recommendationReason, recommendationConfidence ?? "MEDIUM", isComparable ?? true,
                canApply ?? true, hasPendingApplication ?? false, pendingApplicationId)
            : null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string HelpForField(string field) => field switch
    {
        "isWelfareHomeResident" => "An approved welfare home is a formally recognised residential home. Reply yes or no: yes if you live in one, otherwise no.",
        "email" => "Use an email address you can access for FAS notifications, for example student@example.com.",
        "employmentStatusCode" => "Choose the closest option: employed, self-employed, or unemployed.",
        "monthlyHouseholdIncome" => "Use the total monthly income for everyone in your household, in SGD. Example: 3500.",
        "householdMemberCount" => "Count the people currently in your household, including yourself. If a family situation is unclear, use the count you can support on the form and let the school review documents. Reply with one whole number, for example 4.",
        "otherMonthlyIncome" => "Include recurring other monthly income in SGD. Reply 0 if there is no other income.",
        "parentNationalities" => "Choose one option: Singapore Citizen, Permanent Resident, or Foreigner.",
        _ => "Tell me the value shown on your records, or leave it for manual entry on the form."
    };

    private static bool LooksLikeExplicitFasRestart(string message) =>
        Regex.IsMatch(message, @"\b(restart|start over|start again|resume|continue|go back|return|check|qualify|eligib)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(fas|financial assistance|eligibility|check|application)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeContextualResume(string message) =>
        Regex.IsMatch(message, @"^\s*(resume|continue|resume please|continue please|i want to continue|keep going|go back)\s*[.!]?\s*$", RegexOptions.IgnoreCase);

    private static bool LooksLikeWelfareHomeCorrection(string message, bool? currentWelfareHome)
    {
        if (Regex.IsMatch(message, @"\b(welfare|approved home|approved welfare|residing in one|live in one|have it|do have)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        if (!currentWelfareHome.HasValue)
        {
            return false;
        }

        return Regex.IsMatch(message, @"\b(wait|actually|sorry|correction|meant)\b.{0,20}\b(yes|y|no|n)\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeFieldHelpRequest(string message)
    {
        if (Regex.IsMatch(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase))
        {
            return false;
        }

        return Regex.IsMatch(message, @"\b(what are|what is|options|option|choose|choices|example|examples|not sure|don't know|do not know|idk|help|does it count|should i count|count as|which one)\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeUncertaintyAnswer(string message) =>
        Regex.IsMatch(message, @"\b(not sure|don't know|do not know|idk|still not sure|unsure|uncertain)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeFasSchemeGuidanceRequest(string message)
    {
        return Regex.IsMatch(message, @"\b(scheme|schemes|recommend|recommendation|eligible|eligibility|qualify|apply for)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsLiveSchemeEligibilityRequest(string value)
    {
        bool asksWhichSchemes = Regex.IsMatch(value, @"\b(WHICH|WHAT)\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(value, @"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b", RegexOptions.IgnoreCase);
        bool asksApplyOrEligibility = Regex.IsMatch(value, @"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b", RegexOptions.IgnoreCase);
        return asksWhichSchemes && asksApplyOrEligibility;
    }

    private static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? TryGetInt64(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result) ? result : null;
    private static int? TryGetInt32(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : null;
    private static decimal? TryGetDecimal(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal result) ? result : null;
    private static bool? TryGetBoolean(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static FasRecommendationMatch[] WelfareHomeRecommendationMatches(FasInterviewData state)
        => state.ApplicableSchemes
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select((x, index) => new FasRecommendationMatch(x.Id, x.Name, 0, "Welfare-home route", "ASSISTANCE", 0m,
                index + 1, "Open scheme for your school. Welfare-home applicants skip income-based ranking and must review the scheme selection in the form.", "REVIEW_REQUIRED", false))
            .ToArray();

    private static string[] FilterCurrentQuestion(IEnumerable<string> followUps, string message)
    {
        string currentQuestion = Regex.Replace(message.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant);
        return followUps
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !string.Equals(Regex.Replace(x.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant), currentQuestion, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();
    }
}
