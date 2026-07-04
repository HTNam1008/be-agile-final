using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Application.Abstractions.Security;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiOrchestratorService(
    IServiceProvider services, MoeDbContext db, ICurrentUser currentUser, AiFinanceReader finance,
    StudentFasApplicationService fas, IKnowledgeRetriever knowledge, SensitiveDataRedactor redactor,
    AiTurnPlannerService turnPlanner, ILogger<AiOrchestratorService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, CancellationToken ct)
    {
        AiChatRequest sanitizedRequest = new()
        {
            ConversationId = request.ConversationId,
            Message = request.Message.Trim(),
            PageContext = SanitizePageContext(request.PageContext)
        };
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        AiConversation conversation = await GetOrCreateConversation(sanitizedRequest.ConversationId, personId, now, ct);
        string pageJson = sanitizedRequest.PageContext is null ? null! : JsonSerializer.Serialize(sanitizedRequest.PageContext, JsonOptions);
        db.Add(AiMessage.Create(conversation.Id, "USER", redactor.Redact(sanitizedRequest.Message), now));
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            AiTurnPlan turnPlan = await turnPlanner.PlanAsync(sanitizedRequest, conversation, ct);
            if (turnPlan.Intent == AiPlannerIntent.ClarifyFasTypo)
            {
                var clarification = new AiChatResponse(
                    conversation.Id,
                    0,
                    "Did you mean FAS, Financial Assistance Schemes? I can help you check eligibility and prepare the application form.",
                    "GENERAL",
                    new(false, []),
                    [],
                    [new("NAVIGATE", "Open FAS application", "/portal/fas")],
                    null)
                {
                    FollowUpQuestions = ["Yes, help me check FAS eligibility.", "What is FAS?", "What documents do I need for FAS?"],
                    TurnIntent = "CLARIFY_FAS_TYPO",
                    ConversationPhase = turnPlan.Phase
                };
                conversation.Touch("GENERAL", pageJson, conversation.FasInterviewJson, now);
                var clarificationMessage = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(clarification.Text), now,
                    latencyMs: (int)stopwatch.ElapsedMilliseconds, responseJson: SerializeResponse(clarification));
                db.Add(clarificationMessage); await db.SaveChangesAsync(ct);
                return clarification with { MessageId = clarificationMessage.Id };
            }

            turnPlan = NormalizePlannerIntentForCompositeTurn(turnPlan, sanitizedRequest.Message);
            string mode = ModeFromPlan(turnPlan) ?? DetermineMode(sanitizedRequest.Message, conversation.ModeCode, sanitizedRequest.PageContext?.Domain);
            string? interruptedFasStateJson = ApplyFasTaskInterruptBeforeNonFasTurn(conversation.FasInterviewJson, sanitizedRequest.Message, mode);
            if (!string.Equals(interruptedFasStateJson, conversation.FasInterviewJson, StringComparison.Ordinal))
                conversation.Touch(conversation.ModeCode, pageJson, interruptedFasStateJson, now);
            AiChatResponse response = TryHandlePlannerConversationControl(conversation, sanitizedRequest, turnPlan, now)
                ?? mode switch
            {
                "PAYMENT" => await HandlePayment(conversation, sanitizedRequest, now, ct),
                "FAS_INTERVIEW" => await HandleFas(conversation, sanitizedRequest, now, ct),
                _ => await HandleGeneral(conversation, sanitizedRequest, now, ct)
            };
            response = AttachDormantFasState(response, conversation.FasInterviewJson);
            response = AttachFollowUps(response, sanitizedRequest);
            response = AttachV2Metadata(response, turnPlan);
            conversation.Touch(response.Mode, pageJson, conversation.FasInterviewJson, now);
            var assistant = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(response.Text), now,
                JsonSerializer.Serialize(response.Grounding.Citations, JsonOptions),
                JsonSerializer.Serialize(response.Cards.Select(x => x.Type), JsonOptions),
                (int)stopwatch.ElapsedMilliseconds, SerializeResponse(response));
            db.Add(assistant); await db.SaveChangesAsync(ct);
            logger.LogInformation("AI conversation {ConversationId} mode {Mode} completed in {ElapsedMs} ms", conversation.Id, response.Mode, stopwatch.ElapsedMilliseconds);
            return response with { MessageId = assistant.Id };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Guid reviewId = await CreateReview(conversation, personId, "MODEL_OR_TOOL_FAILURE", sanitizedRequest.PageContext, sanitizedRequest.Message, now, ct);
            logger.LogError(ex, "AI conversation {ConversationId} failed after {ElapsedMs} ms", conversation.Id, stopwatch.ElapsedMilliseconds);
            const string text = "I cannot answer this reliably right now, so I will not guess. I can help with Education Account balance, bills, payments, refunds, and FAS application guidance. For anything else, the Admin Center can review your case.";
            var fallbackResponse = new AiChatResponse(conversation.Id, 0, text, "FALLBACK", new(false, []), [], FallbackActions(reviewId), null, reviewId)
            {
                FollowUpQuestions = FallbackFollowUps()
            };
            var fallback = AiMessage.Create(conversation.Id, "ASSISTANT", redactor.Redact(text), now, latencyMs: (int)stopwatch.ElapsedMilliseconds, responseJson: SerializeResponse(fallbackResponse));
            db.Add(fallback); await db.SaveChangesAsync(ct);
            return new AiChatResponse(conversation.Id, fallback.Id, text, "FALLBACK", new(false, []), [],
                FallbackActions(reviewId), null, reviewId)
            {
                FollowUpQuestions = FallbackFollowUps()
            };
        }
    }

    private static AiTurnPlan NormalizePlannerIntentForCompositeTurn(AiTurnPlan plan, string message)
    {
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikePaymentQuery(message.ToUpperInvariant()))
            return plan with { Intent = AiPlannerIntent.PaymentQuery, AnswerGoal = "stop the active FAS task and answer the finance question" };
        if (plan.Intent == AiPlannerIntent.CancelFas && LooksLikeCourseQuestion(message))
            return plan with { Intent = AiPlannerIntent.CourseQuery, AnswerGoal = "stop the active FAS task and answer the course question" };
        return plan;
    }

    private static AiChatResponse? TryHandlePlannerConversationControl(AiConversation conversation, AiChatRequest request, AiTurnPlan plan, DateTime now)
    {
        if (plan.Intent is not (AiPlannerIntent.PauseFas or AiPlannerIntent.CancelFas or AiPlannerIntent.OutOfScopeSmallTalk))
            return null;

        string? pageJson = request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        FasInterviewData? state = DeserializeState(conversation.FasInterviewJson);
        if (state is null)
        {
            return plan.Intent == AiPlannerIntent.OutOfScopeSmallTalk
                ? new(conversation.Id, 0, "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.", "GENERAL", new(false, []), [], [], null)
                {
                    FollowUpQuestions = ["What can you help me with?", "Check if I qualify for FAS.", "Show my Education Account balance."]
                }
                : null;
        }

        if (plan.Intent == AiPlannerIntent.CancelFas)
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            conversation.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            return new(conversation.Id, 0, "Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
            {
                FollowUpQuestions = ["Show my outstanding course bills.", "Restart FAS check.", "What can you help me with?"]
            };
        }

        state.Status = "PAUSED";
        state.ValidationMessage = "FAS check paused before eligibility calculation.";
        conversation.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
        string text = plan.Intent == AiPlannerIntent.OutOfScopeSmallTalk
            ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
            : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.";
        return new(conversation.Id, 0, text, "GENERAL", FasInterviewGrounding(state.Status), [], [], ToInterviewState(state, null))
        {
            FollowUpQuestions = ["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"]
        };
    }

    public async Task<AiConversationResponse> GetConversationAsync(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiConversation conversation = await db.Set<AiConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        var messageRows = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc, x.ResponseJson })
            .ToArrayAsync(ct);
        AiConversationMessageResponse[] messages = messageRows.Select(x => new AiConversationMessageResponse(x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc,
            x.ResponseJson == null ? null : JsonSerializer.Deserialize<object>(x.ResponseJson, JsonOptions))).ToArray();
        return new(conversation.Id, conversation.ModeCode, conversation.StatusCode, messages, DeserializeInterview(conversation.FasInterviewJson));
    }

    public async Task<object> CreateCaseAsync(CreateAdminCenterCaseRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiReviewRecord review = await db.Set<AiReviewRecord>().SingleOrDefaultAsync(x => x.Id == request.ReviewRecordId && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.REVIEW_NOT_FOUND");
        AdminCenterCase item = AdminCenterCase.Create(review.Id, personId, redactor.Redact(request.Description), request.ContactPreference, DateTime.UtcNow);
        db.Add(item); await db.SaveChangesAsync(ct);
        return new { caseId = item.Id, status = item.StatusCode, createdAtUtc = item.CreatedAtUtc };
    }

    private async Task<AiChatResponse> HandlePayment(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        AiFinanceSnapshot snapshot = await finance.GetSnapshotAsync(ct);
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, "PAYMENT");
        string intent = request.Message.ToUpperInvariant();
        var sg = CultureInfo.GetCultureInfo("en-SG");
        string ccy(decimal v) => v.ToString("C", sg);
        if (intent.Contains("HISTORY") || intent.Contains("PAID") || intent.Contains("REFUND"))
        {
            string historyText = snapshot.RecentPayments.Count == 0
                ? "I could not find recent payment records for your account yet. Refunds depend on the payment type and conditions of the original transaction. You can check the Bills & payments page for current outstanding charges."
                : $"You have {snapshot.RecentPayments.Count} recent payment record{(snapshot.RecentPayments.Count == 1 ? "" : "s")}. Refunds are processed based on the original payment method and may take 5-14 business days. Contact your school or Admin Center for refund eligibility.";
            return new(c.Id, 0, historyText, "PAYMENT", Grounding(sources),
                [new("PAYMENT_HISTORY", snapshot.RecentPayments)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")], null);
        }
        if (intent.Contains("WITHDRAW"))
        {
            string withdrawText = "To withdraw from a course, start by reviewing the withdrawal policy on the Bills & payments page. Withdrawals may affect your outstanding charges and Education Account balance. Contact your school for eligibility, deadlines, and any supporting documents needed. You can also reach the Admin Center for further assistance.";
            return new(c.Id, 0, withdrawText, "PAYMENT", Grounding(sources), [],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")], null);
        }
        if (intent.Contains("EDUCATION ACCOUNT") && Regex.IsMatch(intent, @"\b(PAY|USE|USED|FOR|COVER)\b", RegexOptions.IgnoreCase))
        {
            string accountUseText = snapshot.TotalOutstanding <= 0m
                ? $"You have {ccy(snapshot.AvailableBalance)} available in your Education Account. You can use it for eligible course bills and supported student-finance charges when they are issued. You do not have an outstanding bill right now, so there is nothing to pay from it at the moment."
                : $"You have {ccy(snapshot.AvailableBalance)} available in your Education Account. You can use it for eligible course bills and supported student-finance charges. You currently have {ccy(snapshot.TotalOutstanding)} outstanding; open Bills & payments to review what can be paid now.";
            return new(c.Id, 0, accountUseText, "PAYMENT", Grounding(sources),
                [new("FINANCE_SUMMARY", snapshot)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")], null);
        }
        if (Regex.IsMatch(intent, @"\b(HOW|METHOD|OPTION|OPTIONS)\b", RegexOptions.IgnoreCase) && intent.Contains("PAY"))
        {
            string paymentText = snapshot.TotalOutstanding <= 0m
                ? "You do not have an outstanding bill to pay right now. When a bill is due, you can usually pay with available Education Account funds, online payment, or split payment where supported."
                : PaymentOptionsText(snapshot);
            return new(c.Id, 0, paymentText, "PAYMENT", Grounding(sources),
                [new("FINANCE_SUMMARY", snapshot)],
                [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")], null);
        }
        if (intent.Contains("BILL") || intent.Contains("OUTSTANDING") || intent.Contains("DUE"))
        {
            string billText = snapshot.BillCount == 0
                ? "You have no outstanding course bills right now. Check the Bills & payments page for your payment history."
                : $"You have {snapshot.BillCount} outstanding course bill{(snapshot.BillCount == 1 ? "" : "s")} totalling {ccy(snapshot.TotalOutstanding)}. View and pay these on the Bills & payments page.";
            return new(c.Id, 0, billText, "PAYMENT", Grounding(sources),
                [new("OUTSTANDING_BILLS", snapshot.Bills)], [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")], null);
        }
        string paymentStatus = snapshot.TotalOutstanding <= 0
            ? "Nothing is due right now."
            : "Review the available balance and outstanding charges before paying.";
        string text = $"Your live Education Account summary is below, including available balance, outstanding charges, and net available amount.\n\n{paymentStatus}\n\nUse the actions below to open the exact Bills & payments or Education Account view.";
        AiCard card = new("FINANCE_SUMMARY", snapshot);
        AiAction[] actions = [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")];
        return new(c.Id, 0, text, "PAYMENT", Grounding(sources), [card], actions, null);
    }

    private async Task<AiChatResponse> HandleFas(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        bool isNewInterview = c.FasInterviewJson is null;
        string? fieldKey = request.PageContext?.Entity is JsonElement entity &&
            entity.ValueKind == JsonValueKind.Object &&
            entity.TryGetProperty("fieldKey", out JsonElement fk) &&
            fk.ValueKind == JsonValueKind.String &&
            fk.GetString() is string fkStr
            ? fkStr
            : null;
        FasInterviewData state;
        try { state = DeserializeState(c.FasInterviewJson) ?? await InitializeFasState(ct); }
        catch { return new(c.Id, 0, "I couldn't read enough profile information from Singpass to help with FAS. You can still use the FAS form directly, or contact Admin Center for assistance.", "FALLBACK", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], null); }
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
        if (state.Status == "CONFIRMING")
        {
            if (LooksLikeCancelFas(request.Message))
            {
                state.Status = "CANCELLED";
                state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
                AiInterviewState cancelledInterview = ToInterviewState(state, null);
                c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                return new(c.Id, 0, "Got it. I stopped this FAS check and will not calculate eligibility from those answers. You can restart the FAS check later or open the form manually.", "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], cancelledInterview)
                {
                    FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."]
                };
            }

            if (LooksLikeScopeTest(request.Message) || LooksLikeSwitchTopic(request.Message))
            {
                state.Status = "PAUSED";
                state.ValidationMessage = "FAS check paused before eligibility calculation.";
                AiInterviewState pausedInterview = ToInterviewState(state, null);
                string pausedText = LooksLikeScopeTest(request.Message)
                    ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                    : "No problem. I paused this FAS check. Ask me about FAS, your Education Account, bills, payments, refunds, or say \"resume FAS check\" when you want to continue.";
                c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                return new(c.Id, 0, pausedText, "GENERAL", FasInterviewGrounding(state.Status), [], [], pausedInterview)
                {
                    FollowUpQuestions = ["Resume FAS check.", "What is PCI?", "Show my Education Account balance."]
                };
            }

            if (IsFasKnowledgeInterrupt(request.Message.ToUpperInvariant()))
            {
                state.Status = "PAUSED";
                state.ValidationMessage = "FAS check paused while answering a side question.";
                c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                AiChatResponse knowledgeResponse = await HandleGeneral(c, request, now, ct);
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
                c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                return new(c.Id, 0, $"I updated the FAS details.\n\n{ConfirmationPrompt(state)}", "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [], [], correctedInterview);
            }

            FasExtractionResult confirmation = ExtractConfirmation(request.Message);
            if (confirmation.Status == "CLARIFY")
            {
                AiInterviewState confirmInterview = ToInterviewState(state, ConfirmationPrompt(state));
                c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                return new(c.Id, 0, confirmation.Message!, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [], [], confirmInterview);
            }

            if (confirmation.Value is bool confirmed && !confirmed)
            {
                state.Status = "CANCELLED";
                state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
                AiInterviewState manualInterview = ToInterviewState(state, null);
                Guid review = await CreateReview(c, c.PersonId, "FAS_CONFIRMATION_REJECTED", request.PageContext, request.Message, now, ct);
                c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
                return new(c.Id, 0, "No problem. I will not calculate eligibility from these answers. I stopped this FAS check; restart it if you want me to collect and confirm the details again.", "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], manualInterview, review)
                {
                    FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Open FAS application."]
                };
            }

            state.Status = "COLLECTING_CONFIRMED";
            logger.LogInformation("FAS attestation: conversation {Id} confirmed all collected fields via CONFIRMING gate at {Time}. Snapshot: {Snapshot}",
                c.Id, now.ToString("O"), JsonSerializer.Serialize(state, JsonOptions));
        }
        if (!isNewInterview && state.Status == "COMPLETE")
        {
            FasRecommendationMatch[] completedSchemes = state.RecommendationMatches.Count > 0
                ? state.RecommendationMatches.ToArray()
                : state.IsWelfareHomeResident == true ? WelfareHomeRecommendationMatches(state) : [];
            AiInterviewState completedInterview = ToInterviewState(state, null, completedSchemes);
            bool asksForSchemes = IsLiveSchemeEligibilityRequest(request.Message.ToUpperInvariant()) || IsSchemeKbRequest(request.Message);
            string completedText = asksForSchemes && completedSchemes.Length > 0
                ? $"Your confirmed FAS check currently has {completedSchemes.Length} eligible option{(completedSchemes.Length == 1 ? "" : "s")}: {string.Join(", ", completedSchemes.Select(x => x.SchemeName).Distinct(StringComparer.OrdinalIgnoreCase).Take(5))}. Use 'Apply answers to form' to copy the selected actionable schemes, then review the application before submitting."
                : state.IsWelfareHomeResident == true
                ? "You are marked as living in an approved welfare home. I prepared your confirmed details and open FAS scheme selection for the form. Use 'Apply answers to form', then review before submitting."
                : "I have confirmed the details for this FAS check. Use 'Apply answers to form' to copy them into the application, or edit the form manually if anything looks wrong.";
            List<AiAction> completedActions = [new("NAVIGATE", "Open FAS application", "/portal/fas", completedInterview.FormPatch), new("APPLY_FAS_PATCH", "Apply answers to form", Payload: completedInterview.FormPatch)];
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, completedText, "GENERAL", FasInterviewGrounding(state.Status), [], completedActions, completedInterview);
        }

        if (LooksLikeCancelFas(request.Message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            AiInterviewState cancelledInterview = ToInterviewState(state, null);
            c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, "Got it. I stopped this FAS check and will not calculate eligibility from those answers. Ask me about bills, payments, Education Account, or restart FAS later.", "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], cancelledInterview)
            {
                FollowUpQuestions = ["Show my outstanding course bills.", "Restart FAS check.", "What can you help me with?"]
            };
        }

        if (LooksLikeSwitchTopic(request.Message) || LooksLikeScopeTest(request.Message))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused before eligibility calculation.";
            AiInterviewState pausedInterview = ToInterviewState(state, null);
            string pausedText = LooksLikeScopeTest(request.Message)
                ? "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance."
                : "No problem. I paused this FAS check. Ask me about bills, payments, Education Account, FAS policy, or say \"resume FAS check\" when you want to continue.";
            c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, pausedText, "GENERAL", FasInterviewGrounding(state.Status), [], [], pausedInterview)
            {
                FollowUpQuestions = ["Resume FAS check.", "Show my outstanding course bills.", "What is PCI?"]
            };
        }

        if (LooksLikePaymentQuery(request.Message.ToUpperInvariant()) || LooksLikeCourseQuestion(request.Message))
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused while answering a side question.";
            c.Touch("GENERAL", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            AiChatResponse sideResponse = LooksLikePaymentQuery(request.Message.ToUpperInvariant())
                ? await HandlePayment(c, request, now, ct)
                : await HandleGeneral(c, request, now, ct);
            return sideResponse with
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
            Guid review = await CreateReview(c, c.PersonId, "FAS_MANUAL_FALLBACK", request.PageContext, request.Message, now, ct);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [],
                [new("NAVIGATE", "Open FAS application", "/portal/fas"), new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })],
                manualInterview, review);
        }

        if (extraction.Status == "CLARIFY")
        {
            state.Status = "CLARIFYING";
            AiInterviewState clarificationInterview = ToInterviewState(state, extraction.Message);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [], [], clarificationInterview);
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
                    recommendedSchemes = ReviewRequiredSchemeMatches(state);
                    state.RecommendationMatches = recommendedSchemes.ToList();
                    AiInterviewState completeInterview = ToInterviewState(state, null, recommendedSchemes);
                    recommendation = BuildReviewRequiredRecommendation(completeInterview, recommendedSchemes);
                    text = $"I found {recommendedSchemes.Length} open FAS scheme{(recommendedSchemes.Length == 1 ? "" : "s")} for your school. The scheme criteria are not fully configured in the demo data, so I prepared your confirmed answers and scheme selection for review. Use 'Apply answers to form', then check the form before submitting.";
                }
                else if (!hasSchemes) { state.Status = "MANUAL_FALLBACK"; text = "Based on your details, I could not find an eligible FAS scheme. Use 'Open FAS application' to review the form, or 'Contact Admin Center' below for staff help."; }
                else
                {
                    state.Status = "COMPLETE";
                    recommendedSchemes = ExtractRecommendationMatches(root);
                    state.RecommendationMatches = recommendedSchemes.ToList();
                    AiInterviewState completeInterview = ToInterviewState(state, null, recommendedSchemes);
                    recommendation = BuildFasRecommendation(root, completeInterview);
                    text = "I have enough information to evaluate the active FAS schemes. Review the recommendation below and use 'Apply answers to form' when ready.";
                }
            }
            catch
            {
                if (CanPrepareOpenSchemeForReview(state))
                {
                    state.Status = "COMPLETE";
                    recommendedSchemes = ReviewRequiredSchemeMatches(state);
                    state.RecommendationMatches = recommendedSchemes.ToList();
                    AiInterviewState completeInterview = ToInterviewState(state, null, recommendedSchemes);
                    recommendation = BuildReviewRequiredRecommendation(completeInterview, recommendedSchemes);
                    text = $"I found {recommendedSchemes.Length} open FAS scheme{(recommendedSchemes.Length == 1 ? "" : "s")} for your school. The scheme criteria are not fully configured in the demo data, so I prepared your confirmed answers and scheme selection for review. Use 'Apply answers to form', then check the form before submitting.";
                }
                else
                {
                    state.Status = "MANUAL_FALLBACK";
                    text = "Based on your details, I could not find an eligible FAS scheme. Use 'Open FAS application' to review the form, or 'Contact Admin Center' below for staff help.";
                }
            }
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
        c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
        List<AiCard> cards = recommendation is null ? [] : [new("FAS_RECOMMENDATION", recommendation)];
        Guid? fallbackReview = null;
        if (state.Status == "MANUAL_FALLBACK")
            fallbackReview = await CreateReview(c, c.PersonId, "FAS_MANUAL_FALLBACK", request.PageContext, request.Message, now, ct);
        List<AiAction> actions = state.Status == "COMPLETE"
            ? [new("NAVIGATE", "Open FAS application", "/portal/fas", interview.FormPatch)]
            : state.Status == "MANUAL_FALLBACK"
                ? [new("NAVIGATE", "Open FAS application", "/portal/fas")]
                : [];
        if (fallbackReview.HasValue) actions.Add(new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = fallbackReview.Value }));
        if (state.Status == "COMPLETE") actions.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: interview.FormPatch));
        string mode = state.Status == "COMPLETE" ? "GENERAL" : "FAS_INTERVIEW";
        return new(c.Id, 0, text, mode, FasInterviewGrounding(state.Status), cards, actions, interview, fallbackReview);
    }

    private async Task<AiChatResponse> HandleGeneral(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        if (LooksLikeScopeTest(request.Message))
        {
            return new(c.Id, 0, "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.", "GENERAL", new(false, []), [], [], null)
            {
                FollowUpQuestions = ["What can you help me with?", "Check if I qualify for FAS.", "Show my Education Account balance."]
            };
        }

        if (LooksLikeCapabilityQuestion(request.Message))
        {
            const string capabilityText = "I can help with Education Account balance, outstanding bills, payment history, refunds, and FAS guidance. I can also walk you through a FAS eligibility check before you open the application form.";
            AiAction[] capabilityActions =
            [
                new("NAVIGATE", "Open Bills & payments page", "/portal/bills"),
                new("NAVIGATE", "Open FAS application", "/portal/fas")
            ];
            return new(c.Id, 0, capabilityText, "GENERAL", new(false, []), [], capabilityActions, null)
            {
                FollowUpQuestions = ["Show my Education Account balance.", "Check if I qualify for FAS.", "What documents do I need for FAS?"]
            };
        }

        if (LooksLikeCourseQuestion(request.Message))
        {
            const string courseText = "I can help with course-related finance questions, such as outstanding course bills, payment options, and how FAS may apply to eligible course charges. For course enrolment details, open the Courses page.";
            return new(c.Id, 0, courseText, "GENERAL", new(false, []), [], [new("NAVIGATE", "Open Courses page", "/portal/courses"), new("NAVIGATE", "Open Bills & payments page", "/portal/bills")], null)
            {
                FollowUpQuestions = ["Show my outstanding course bills.", "How do I pay this bill?", "Check if I qualify for FAS."]
            };
        }

        if (LooksLikeAdminCenterQuestion(request.Message))
        {
            const string adminText = "Admin Center can review questions the copilot cannot answer safely, such as unusual FAS circumstances, disputed bills, refund issues, or application details that need staff judgement.";
            return new(c.Id, 0, adminText, "GENERAL", new(false, []), [], [new("CONTACT_ADMIN_CENTER", "Contact Admin Center")], null)
            {
                FollowUpQuestions = ["Check if I qualify for FAS.", "Show my outstanding course bills.", "What can you help me with?"]
            };
        }

        bool isFasKnowledgeRequest = IsSchemeKbRequest(request.Message) || IsFasKnowledgeInterrupt(request.Message) || LooksLikeNaturalFasAidQuestion(request.Message);
        string retrievalDomain = isFasKnowledgeRequest ? "FAS" : request.PageContext?.Domain ?? "GENERAL";
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, retrievalDomain);
        if (sources.Count == 0)
        {
            Guid review = await CreateReview(c, c.PersonId, "MISSING_POLICY", request.PageContext, request.Message, now, ct);
            const string fallbackText = "I cannot answer this reliably from the reviewed MOE student-finance guidance, so I will not guess. I can help with Education Account balance, bills, payments, refunds, and FAS application guidance. Use Contact Admin Center below if you want a staff review.";
            return new(c.Id, 0, fallbackText, "FALLBACK", new(false, []), [], FallbackActions(review), null, review)
            {
                FollowUpQuestions = FallbackFollowUps()
            };
        }

        KnowledgeAnswerCard knowledgeCard = BuildKnowledgeAnswerCard(request.Message, sources);
        string sourceText = string.Join("\n", sources.Select(x => $"[{x.Citation.SourceId}] ({x.Citation.SourceStatus}) {x.Content}"));
        if (isFasKnowledgeRequest && sources.Count > 0)
        {
            string deterministicText = BuildKnowledgeAnswer(request.Message, sources);
            return new(c.Id, 0, deterministicText, "GENERAL", Grounding(sources), [new("KNOWLEDGE_ANSWER", knowledgeCard)], KnowledgeActions(sources), null)
            {
                FollowUpQuestions = ContextualKnowledgeFollowUps(c, request.Message, knowledgeCard.FollowUpQuestions)
            };
        }
        var history = new ChatHistory(
            "You are the MOE Student Finance Copilot. Answer like a calm counter officer, not a policy document.\n" +
            "Keep the answer under 120 words. Lead with the direct answer. Ask at most one next question.\n" +
            "Use no more than three bullets. Do not include source IDs, bracket citations, or raw document codes in the answer text; the UI renders sources separately.\n" +
            "Never invent personal data, policy, eligibility, amounts, status, or timelines. If the question is outside student finance or FAS, say what you can help with instead.\n" +
            "Label prototype uncertainty in plain language only when it affects the answer.\n" +
            $"Sources:\n{sourceText}");
        history.AddUserMessage(request.Message);
        Kernel kernel = services.GetRequiredService<Kernel>();
        ChatMessageContent answer = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
        string text = string.IsNullOrWhiteSpace(answer.Content) ? "I do not have enough reliable information to answer that." : answer.Content.Trim();
        if (sources.Any(x => x.Citation.SourceStatus == "PROTOTYPE"))
        {
            text += "\n\nSome parts of this answer are based on prototype guidance and may change. Use the actions below when you want to continue in the portal.";
        }
        return new(c.Id, 0, text, "GENERAL", Grounding(sources), [new("KNOWLEDGE_ANSWER", knowledgeCard)], KnowledgeActions(sources), null)
        {
            FollowUpQuestions = ContextualKnowledgeFollowUps(c, request.Message, knowledgeCard.FollowUpQuestions)
        };
    }

    private async Task<AiChatResponse> HandleStoppedFasTurn(AiConversation c, AiChatRequest request, FasInterviewData state, DateTime now, CancellationToken ct)
    {
        string pageJson = request.PageContext is null ? null! : JsonSerializer.Serialize(request.PageContext, JsonOptions);
        bool isCancelled = state.Status == "CANCELLED";

        if (LooksLikeCancelFas(request.Message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, "This FAS check is already stopped. I will not calculate eligibility from those answers unless you restart the check.", "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
            {
                FollowUpQuestions = ["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."]
            };
        }

        if (LooksLikeScopeTest(request.Message))
        {
            if (!isCancelled)
            {
                state.Status = "PAUSED";
                state.ValidationMessage = "FAS check paused before eligibility calculation.";
            }

            c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, "I can't help with jokes here. I can help with FAS, Education Account balance, bills, payments, refunds, or application guidance.", "GENERAL", FasInterviewGrounding(state.Status), [], [], ToInterviewState(state, null))
            {
                FollowUpQuestions = isCancelled
                    ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."]
                    : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."]
            };
        }

        if (IsFasKnowledgeInterrupt(request.Message.ToUpperInvariant()) || LooksLikeCapabilityQuestion(request.Message) || LooksLikeAdminCenterQuestion(request.Message))
        {
            c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            AiChatResponse response = await HandleGeneral(c, request, now, ct);
            return response with
            {
                InterviewState = ToInterviewState(state, null),
                FollowUpQuestions = isCancelled
                    ? FilterCurrentQuestion(["Restart FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], request.Message)
                    : FilterCurrentQuestion(["Resume FAS check.", "What documents do I need for FAS?", "Show my Education Account balance."], request.Message)
            };
        }

        if (LooksLikePaymentQuery(request.Message.ToUpperInvariant()))
        {
            c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            AiChatResponse response = await HandlePayment(c, request, now, ct);
            return response with { InterviewState = ToInterviewState(state, null) };
        }

        if (ExtractConfirmation(request.Message).Value is bool)
        {
            string text = isCancelled
                ? "That previous FAS check was stopped, so I will not treat this as confirmation. Say \"restart FAS check\" if you want to run a new eligibility check."
                : "The FAS check is paused. Say \"resume FAS check\" when you want to return to the confirmation step.";
            c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, text, "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
            {
                FollowUpQuestions = isCancelled
                    ? ["Restart FAS check.", "Open FAS application.", "What documents do I need for FAS?"]
                    : ["Resume FAS check.", "Open FAS application.", "What documents do I need for FAS?"]
            };
        }

        c.Touch("GENERAL", pageJson, JsonSerializer.Serialize(state, JsonOptions), now);
        return new(c.Id, 0, isCancelled
            ? "This FAS check is stopped. Ask a student-finance question, open the FAS form, or say \"restart FAS check\" to begin again."
            : "This FAS check is paused. Ask a student-finance question, or say \"resume FAS check\" to continue.",
            "GENERAL", FasInterviewGrounding(state.Status), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], ToInterviewState(state, null))
        {
            FollowUpQuestions = isCancelled
                ? ["Restart FAS check.", "What can you help me with?", "Show my Education Account balance."]
                : ["Resume FAS check.", "What can you help me with?", "Show my Education Account balance."]
        };
    }

    private static string? ApplyFasTaskInterruptBeforeNonFasTurn(string? fasInterviewJson, string message, string mode)
    {
        if (mode == "FAS_INTERVIEW" || string.IsNullOrWhiteSpace(fasInterviewJson))
            return fasInterviewJson;

        FasInterviewData? state = DeserializeState(fasInterviewJson);
        if (state is null || state.Status is "COMPLETE" or "CANCELLED")
            return fasInterviewJson;

        if (LooksLikeCancelFas(message))
        {
            state.Status = "CANCELLED";
            state.ValidationMessage = "FAS check stopped by user before eligibility calculation.";
            return JsonSerializer.Serialize(state, JsonOptions);
        }

        if (LooksLikeSwitchTopic(message) || LooksLikeScopeTest(message) || mode == "PAYMENT")
        {
            state.Status = "PAUSED";
            state.ValidationMessage = "FAS check paused before eligibility calculation.";
            return JsonSerializer.Serialize(state, JsonOptions);
        }

        return fasInterviewJson;
    }

    private static string[] ContextualKnowledgeFollowUps(AiConversation conversation, string message, IReadOnlyCollection<string> followUps)
    {
        const string resumeInterview = "Continue my FAS eligibility check.";
        bool hasInterruptedInterview = conversation.ModeCode == "FAS_INTERVIEW" && !string.IsNullOrWhiteSpace(conversation.FasInterviewJson);
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
        Regex.Replace(value.Trim().TrimEnd('.', '?', '!'), @"\s+", " ", RegexOptions.CultureInvariant);

    private static IReadOnlyCollection<AiAction> KnowledgeActions(IReadOnlyList<KnowledgeResult> sources)
    {
        bool hasFasSource = sources.Any(source => source.Citation.SourceId.StartsWith("FAS-", StringComparison.OrdinalIgnoreCase));
        return hasFasSource ? [new("NAVIGATE", "Open FAS application", "/portal/fas")] : [];
    }

    private static string BuildKnowledgeAnswer(string question, IReadOnlyList<KnowledgeResult> sources)
    {
        if (LooksLikeFormulaQuestion(question))
        {
            return FormulaAnswer(question);
        }
        if (LooksLikeHouseholdIncomeDefinitionQuestion(question))
        {
            return "Household income usually means the combined monthly income of household members, including employment income and other regular income such as rental, dividend, or investment income where applicable.";
        }
        if (LooksLikeDocumentQuestion(question))
        {
            KnowledgeAnswerCard documentCard = BuildKnowledgeAnswerCard(question, sources);
            string firstFact = documentCard.Summary;
            return string.IsNullOrWhiteSpace(firstFact)
                ? "Before submitting FAS, prepare the income and supporting documents requested by the institution, then review the form before submission."
                : $"Before submitting FAS, prepare the requested supporting documents. {firstFact}";
        }

        KnowledgeResult primary = sources[0];
        KnowledgeAnswerCard card = BuildKnowledgeAnswerCard(question, sources);
        string topic = primary.Citation.Section;
        return $"I found reviewed guidance for {topic}. I kept the details in the card below so the answer is easier to scan.";
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

    private static KnowledgeAnswerCard BuildKnowledgeAnswerCard(string question, IReadOnlyList<KnowledgeResult> sources)
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
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 360 ? text : $"{text[..360].TrimEnd()}...";
    }

    private static IEnumerable<string> KnowledgeLines(string content) => content.Split('\n')
        .Select(line => Regex.Replace(line.Trim(), @"^[#*\-\s|>]+", "").Trim())
        .Where(line => line.Length > 0 && !line.Contains("---", StringComparison.Ordinal) && !line.StartsWith("|", StringComparison.Ordinal))
        .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
        .Where(line => line.Length > 0);

    private static bool LooksLikeInstructionHeading(string line) =>
        Regex.IsMatch(line, @"^(answer|do not|source|notes?|scope|in scope|explicitly out of scope)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeCapabilityQuestion(string message) =>
        !IsFasQuestion(message) &&
        Regex.IsMatch(message, @"\b(what can you help|what do you do|help me with|your capabilities|what can i ask)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeAdminCenterQuestion(string message) =>
        Regex.IsMatch(message, @"\b(how can admin center help|what can admin center do|admin center help)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeFormulaQuestion(string message) =>
        LooksLikePciQuestion(message) ||
        (Regex.IsMatch(message, @"\b(GHI|GROSS HOUSEHOLD INCOME)\b", RegexOptions.IgnoreCase) &&
         Regex.IsMatch(message, @"\b(CALCULAT\w*|FORMULA|HOW|WHAT|DEFINE|MEAN)\b", RegexOptions.IgnoreCase)) ||
        (Regex.IsMatch(message, @"\b(SUBSIDY|BURSARY)\b", RegexOptions.IgnoreCase) &&
         Regex.IsMatch(message, @"\b(RATE|CALCULAT\w*|DETERMINE|HOW|FORMULA|TIER)\b", RegexOptions.IgnoreCase));

    private static bool LooksLikePciQuestion(string message) =>
        Regex.IsMatch(message, @"\b(PCI|PER[-\s]?CAPITA|PER CAPITA INCOME)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(CALCULAT\w*|FORMULA|HOW|WHAT|MEAN)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeHouseholdIncomeDefinitionQuestion(string message) =>
        Regex.IsMatch(message, @"\b(what|which|count|counts|included|include)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(household income|income for fas|fas income)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeDocumentQuestion(string message) =>
        Regex.IsMatch(message, @"\b(document|documents|proof|payslip|cpf|iras|supporting)\b", RegexOptions.IgnoreCase);

    private static IEnumerable<string> KnowledgeFacts(string content)
    {
        foreach (string line in KnowledgeLines(content).Where(line => !LooksLikeInstructionHeading(line)))
        {
            foreach (string sentence in Regex.Split(line, @"(?<=[.!?])\s+"))
            {
                string value = sentence.Trim();
                if (value.Length > 0)
                    yield return value;
            }
        }
    }

    private static string? ModeFromPlan(AiTurnPlan plan) => plan.Intent switch
    {
        AiPlannerIntent.PaymentQuery => "PAYMENT",
        AiPlannerIntent.AnswerKnowledge or AiPlannerIntent.CourseQuery or AiPlannerIntent.CancelFas or
            AiPlannerIntent.PauseFas or AiPlannerIntent.SwitchTopic or AiPlannerIntent.OutOfScopeSmallTalk => "GENERAL",
        AiPlannerIntent.StartFas or AiPlannerIntent.ContinueFas => "FAS_INTERVIEW",
        _ => null
    };

    private static AiChatResponse AttachV2Metadata(AiChatResponse response, AiTurnPlan plan)
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

    private static AiChatResponse AttachDormantFasState(AiChatResponse response, string? fasInterviewJson)
    {
        if (response.InterviewState is not null)
            return response;

        FasInterviewData? state = DeserializeState(fasInterviewJson);
        return state is not null && IsTerminalFasState(state.Status)
            ? response with { InterviewState = ToInterviewState(state, null) }
            : response;
    }

    private static IReadOnlyCollection<AiCard> AttachFasTaskStateCard(IReadOnlyCollection<AiCard> cards, AiInterviewState? interview, string phase)
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

    private static string IntentLabel(AiPlannerIntent intent) => intent switch
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

    private static IEnumerable<string> SelectKnowledgeFacts(string question, string content)
    {
        string lower = question.ToLowerInvariant();
        string[] facts = KnowledgeFacts(content).ToArray();
        if (lower.Contains("document") || lower.Contains("proof") || lower.Contains("payslip") || lower.Contains("cpf") || lower.Contains("iras"))
        {
            string[] documentFacts = facts
                .Where(f => Regex.IsMatch(f, @"\b(document|income proof|cpf|iras|payslip|assessment|supporting|attach|declare|rental|dividend|investment)\b", RegexOptions.IgnoreCase))
                .ToArray();
            if (documentFacts.Length > 0)
                return documentFacts;
        }

        return facts;
    }

    private static string StripBoldMarkers(string text) =>
        Regex.Replace(text, @"\*{1,2}", "");

    private async Task<AiConversation> GetOrCreateConversation(Guid? id, long personId, DateTime now, CancellationToken ct)
    {
        if (id.HasValue)
        {
            AiConversation? existing = await db.Set<AiConversation>().SingleOrDefaultAsync(x => x.Id == id.Value, ct);
            if (existing is not null && existing.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");
            if (existing is not null) return existing;
        }
        AiConversation created = AiConversation.Start(id ?? Guid.NewGuid(), personId, now); db.Add(created); return created;
    }

    private async Task<Guid> CreateReview(AiConversation c, long personId, string reason, AiPageContext? page, string transcript, DateTime now, CancellationToken ct)
    {
        AiReviewRecord record = AiReviewRecord.Create(c.Id, personId, reason, page?.Domain ?? "GENERAL", page?.Path, redactor.Redact(transcript), now);
        db.Add(record); await db.SaveChangesAsync(ct); return record.Id;
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

    private static string DetermineMode(string message, string current, string? domain)
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

    private static bool IsTerminalFasState(string status) =>
        status is "CANCELLED" or "PAUSED" or "MANUAL_FALLBACK";

    private static bool LooksLikeExplicitFasRestart(string message) =>
        Regex.IsMatch(message, @"\b(restart|start over|start again|resume|continue|go back|return|check|qualify|eligib)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(fas|financial assistance|eligibility|check|application)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeContextualResume(string message) =>
        Regex.IsMatch(message, @"^\s*(resume|continue|resume please|continue please|i want to continue|keep going|go back)\s*[.!]?\s*$", RegexOptions.IgnoreCase);

    private static bool LooksLikeCancelFas(string message) =>
        Regex.IsMatch(message, @"\b(stop|cancel|quit|end|drop|don't want|dont want|do not want|no longer|not doing|forget)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(message, @"\b(fas|financial assistance|eligibility|check|application|this|anymore|now)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeSwitchTopic(string message) =>
        Regex.IsMatch(message, @"\b(ask something else|something else|different question|change topic|another question)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikeScopeTest(string message) =>
        Regex.IsMatch(message, @"\b(tell me (a )?joke|make me laugh|sing|poem|roleplay|story|weather|recipe|movie)\b", RegexOptions.IgnoreCase);

    private static bool LooksLikePaymentQuery(string value) =>
        value.Contains("PAY", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("BILL", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("BALANCE", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("OUTSTANDING", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("REFUND", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("WITHDRAW", StringComparison.OrdinalIgnoreCase) ||
        (value.Contains("EDUCATION ACCOUNT", StringComparison.OrdinalIgnoreCase) &&
         Regex.IsMatch(value, @"\b(USE|USED|FOR|COVER|PAY)\b", RegexOptions.IgnoreCase));

    private static bool LooksLikeCourseQuestion(string message) =>
        Regex.IsMatch(message, @"^\s*(courses?|course\?)\s*$", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(message, @"\b(course|courses|enrolment|enrollment|class|classes)\b", RegexOptions.IgnoreCase);

    private static bool IsContinueInterviewRequest(string value) =>
        Regex.IsMatch(value, @"\b(CONTINUE|RESUME|GO BACK|KEEP GOING|FINISH)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(FAS|FINANCIAL ASSISTANCE|ELIGIBILITY|CHECK|APPLICATION|INTERVIEW)\b", RegexOptions.IgnoreCase);

    private static bool IsLikelyInterviewAnswer(string value) =>
        Regex.IsMatch(value, @"^\s*(?:yes|no|y|n|\d[\d,]*(?:\.\d+)?|none|nil|zero|singapore(?:an| citizen)?|foreigner|permanent resident|pr)\s*\.?\s*$",
            RegexOptions.IgnoreCase);

    private static bool IsSchemeKbRequest(string value)
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

    private static bool IsLiveSchemeEligibilityRequest(string value)
    {
        bool asksWhichSchemes = Regex.IsMatch(value, @"\b(WHICH|WHAT)\b", RegexOptions.IgnoreCase) &&
            Regex.IsMatch(value, @"\b(SCHEME|SCHEMES|FAS|FINANCIAL ASSISTANCE|BURSARY|SUBSIDY)\b", RegexOptions.IgnoreCase);
        bool asksApplyOrEligibility = Regex.IsMatch(value, @"\b(CAN I APPLY|APPLY FOR|ELIGIB|QUALIF|AVAILABLE TO ME|FOR ME)\b", RegexOptions.IgnoreCase);
        return asksWhichSchemes && asksApplyOrEligibility;
    }

    private static bool IsFasKnowledgeInterrupt(string value)
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

    private static bool LooksLikeNaturalFasAidQuestion(string value) =>
        Regex.IsMatch(value, @"\b(help|support|aid|assistance|subsidy|bursary)\b", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(value, @"\b(school fees?|course fees?|education costs?|school costs?|fees?|family|household|income|earn|afford)\b", RegexOptions.IgnoreCase);

    private static bool IsFasInterviewRequest(string value)
    {
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE");
        bool asksForInterview = Regex.IsMatch(value, @"\b(APPLY|APPLICATION|CHECK|ELIGIB|QUALIF|ASSESS|START|HELP|GUIDE|WANT|DO|WALK|TELL|SHOW|LEARN|KNOW|ASSIST|HOW|QUESTION)\b", RegexOptions.IgnoreCase);
        bool eligibilityWithoutFas = (value.Contains("ELIGIB") || value.Contains("QUALIF")) && mentionsFas;
        return eligibilityWithoutFas || (mentionsFas && asksForInterview);
    }

    private static readonly HashSet<string> AllowedFieldKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "isWelfareHomeResident", "monthlyHouseholdIncome", "householdMemberCount",
        "parentNationalities", "employmentStatusCode", "otherMonthlyIncome", "email"
    };

    private static AiPageContext? SanitizePageContext(AiPageContext? pageContext)
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

    private static bool IsAllowedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) return false;
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal)) return false;
        return AllowedRoutePrefixes.Any(prefix =>
            path.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith($"{prefix}/", StringComparison.OrdinalIgnoreCase));
    }

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

    private static FasRecommendationMatch[] WelfareHomeRecommendationMatches(FasInterviewData state)
        => state.ApplicableSchemes
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select((x, index) => new FasRecommendationMatch(x.Id, x.Name, 0, "Welfare-home route", "ASSISTANCE", 0m,
                index + 1, "Open scheme for your school. Welfare-home applicants skip income-based ranking and must review the scheme selection in the form.", "REVIEW_REQUIRED", false))
            .ToArray();

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

    private static bool IsReadyForEligibilityComputation(FasInterviewData s) => s.Status == "COLLECTING_CONFIRMED";

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

    private static bool FieldCountsAsMissing(FasInterviewData s, string fieldName, bool confirmed)
    {
        if (confirmed) return false;
        if (s.IsWelfareHomeResident == true && fieldName is "monthlyHouseholdIncome" or "householdMemberCount" or "otherMonthlyIncome") return false;
        if (fieldName is "monthlyHouseholdIncome" or "householdMemberCount" or "otherMonthlyIncome") return IncomeFactsRequired(s);
        if (fieldName == "parentNationalities") return ParentNationalityRequired(s);
        if (fieldName is "email" or "employmentStatusCode") return false;
        return true;
    }

    private static bool IncomeFactsRequired(FasInterviewData s) =>
        CriteriaPlanUnknown(s) || s.RequiredCriteriaTypes.Any(IsIncomeCriterion) || s.ApplicableSchemes.Count > 0;
    private static bool ParentNationalityRequired(FasInterviewData _) => true;
    private static bool CriteriaPlanUnknown(FasInterviewData s) => s.RequiredCriteriaTypes.Count == 0 && s.ApplicableSchemeNames.Count == 0;
    private static bool IsIncomeCriterion(string criteriaType) => criteriaType is "GDP" or "GHI" or "PCI";

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

    private static FasExtractionResult ExtractEmail(string message)
    {
        Match match = Regex.Match(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success
            ? FasExtractionResult.Accepted(match.Value)
            : FasExtractionResult.Clarify("Please provide a valid email address, for example student@example.com.");
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

    private static FasExtractionResult ExtractOtherIncome(string message)
    {
        if (Regex.IsMatch(message, @"\b(none|no other|nothing|nil|zero)\b", RegexOptions.IgnoreCase))
            return FasExtractionResult.Accepted(0m);
        FasExtractionResult result = ExtractIncome(message);
        return result.Status == "CLARIFY" && result.Message is not null
            ? FasExtractionResult.Clarify(result.Message.Replace("total monthly household income", "other monthly household income", StringComparison.OrdinalIgnoreCase))
            : result;
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

    private static string ParentNationalityClarification() =>
        "Choose one of these parent or guardian nationality options: Singapore Citizen, Permanent Resident, or Foreigner.";

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
    private static FasInterviewData? DeserializeState(string? value) => string.IsNullOrWhiteSpace(value) ? null : JsonSerializer.Deserialize<FasInterviewData>(value, JsonOptions);
    private static AiInterviewState? DeserializeInterview(string? value) => DeserializeState(value) is { } state ? ToInterviewState(state, NextQuestion(state)) : null;
    private static AiGrounding Grounding(IReadOnlyList<KnowledgeResult> sources) => new(sources.Count > 0, sources.Select(x => x.Citation).ToArray());
    private static AiGrounding FasInterviewGrounding(string _) => new(false, []);
    private static AiAction[] FallbackActions(Guid review) =>
    [
        new("NAVIGATE", "Education Account FAQ", "/portal/account"),
        new("NAVIGATE", "Payment FAQ", "/portal/bills"),
        new("NAVIGATE", "FAS FAQ", "/portal/fas"),
        new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })
    ];

    private static string PaymentOptionsText(AiFinanceSnapshot snapshot)
    {
        if (snapshot.TotalOutstanding <= 0m) return "Nothing is due right now.";
        var info = CultureInfo.GetCultureInfo("en-SG");
        string f(decimal v) => v.ToString("C", info);
        if (snapshot.AvailableBalance >= snapshot.TotalOutstanding)
            return "Your Education Account balance covers the outstanding amount. Review the bill details before paying, and settle any charges before enrolling in new courses.";
        if (snapshot.AvailableBalance > 0m)
        {
            decimal remainder = snapshot.TotalOutstanding - snapshot.AvailableBalance;
            return $"Your Education Account covers part of the outstanding amount but is short by {f(remainder)}. Use split payment or another online method for the remainder where supported.";
        }
        return "Your Education Account does not have available funds for this amount. Use another online payment method where supported.";
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

    private static AiChatResponse AttachFollowUps(AiChatResponse response, AiChatRequest request)
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
            "GENERAL" => IsFasQuestion(message) ? FasKnowledgeFollowUps(message) : GeneralFinanceFollowUps(),
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

    private static bool IsFasQuestion(string message) =>
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

    private static string SerializeResponse(AiChatResponse response)
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

    private sealed class FasInterviewData
    {
        public string Status { get; set; } = "COLLECTING";
        public JsonElement Profile { get; set; }
        public bool? IsWelfareHomeResident { get; set; }
        public string? Email { get; set; }
        public string? EmploymentStatusCode { get; set; }
        public decimal? MonthlyHouseholdIncome { get; set; }
        public int? HouseholdMemberCount { get; set; }
        public decimal? OtherMonthlyIncome { get; set; }
        public List<string> ParentNationalities { get; set; } = [];
        public List<FasApplicableSchemeOption> ApplicableSchemes { get; set; } = [];
        public List<string> ApplicableSchemeNames { get; set; } = [];
        public List<string> RequiredCriteriaTypes { get; set; } = [];
        public List<string> ProfileConfirmedFacts { get; set; } = [];
        public List<string> UserRequiredFacts { get; set; } = [];
        public List<FasRecommendationMatch> RecommendationMatches { get; set; } = [];
        public string? ClarificationField { get; set; }
        public string? ValidationMessage { get; set; }
        public string? PendingParentNationalitySuggestion { get; set; }
        public Dictionary<string, int> ClarificationAttempts { get; set; } = [];
    }

    private sealed record FasExtractionResult(string Status, object? Value = null, string? Message = null)
    {
        public static FasExtractionResult Accepted(object? value = null) => new("ACCEPTED", value);
        public static FasExtractionResult Clarify(string message) => new("CLARIFY", Message: message);
        public static FasExtractionResult ManualFallback(string message) => new("MANUAL_FALLBACK", Message: message);
    }

    private sealed record FasApplicableSchemeOption(long Id, string Name);
}
