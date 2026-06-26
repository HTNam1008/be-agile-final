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
    ILogger<AiOrchestratorService> logger)
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
            string mode = DetermineMode(sanitizedRequest.Message, conversation.ModeCode, sanitizedRequest.PageContext?.Domain);
            AiChatResponse response = mode switch
            {
                "PAYMENT" => await HandlePayment(conversation, sanitizedRequest, now, ct),
                "FAS_INTERVIEW" => await HandleFas(conversation, sanitizedRequest, now, ct),
                _ => await HandleGeneral(conversation, sanitizedRequest, now, ct)
            };
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
            const string text = "I could not complete that request reliably. I've stopped making guesses here — the portal pages and Admin Center remain the official ways to proceed.";
            var fallbackResponse = new AiChatResponse(conversation.Id, 0, text, "FALLBACK", new(false, []), [], FallbackActions(reviewId), null, reviewId);
            var fallback = AiMessage.Create(conversation.Id, "ASSISTANT", text, now, latencyMs: (int)stopwatch.ElapsedMilliseconds, responseJson: SerializeResponse(fallbackResponse));
            db.Add(fallback); await db.SaveChangesAsync(ct);
            return new AiChatResponse(conversation.Id, fallback.Id, text, "FALLBACK", new(false, []), [],
                FallbackActions(reviewId), null, reviewId);
        }
    }

    public async Task<AiConversationResponse> GetConversationAsync(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        AiConversation conversation = await db.Set<AiConversation>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        AiConversationMessageResponse[] messages = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc).Select(x => new AiConversationMessageResponse(x.Id, x.RoleCode, x.ContentRedacted, x.CreatedAtUtc,
                x.ResponseJson == null ? null : JsonSerializer.Deserialize<object>(x.ResponseJson, JsonOptions))).ToArrayAsync(ct);
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
        if (intent.Contains("BILL") || intent.Contains("OUTSTANDING") || intent.Contains("DUE"))
        {
            string billText = snapshot.BillCount == 0
                ? "You have no outstanding course bills right now. Check the Bills & payments page for your payment history."
                : $"You have {snapshot.BillCount} outstanding course bill{(snapshot.BillCount == 1 ? "" : "s")} totalling {ccy(snapshot.TotalOutstanding)}. View and pay these on the Bills & payments page.";
            return new(c.Id, 0, billText, "PAYMENT", Grounding(sources),
                [new("OUTSTANDING_BILLS", snapshot.Bills)], [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")], null);
        }
        string text = $"Your Education Account balance is {ccy(snapshot.AvailableBalance)}, with {ccy(snapshot.TotalOutstanding)} in outstanding charges. That leaves {ccy(snapshot.NetAvailable)} available to use.\n\n{PaymentOptionsText(snapshot)}\n\nYou can see the full breakdown on the Bills & payments or Education Account page.";
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
        if (!isNewInterview && state.Status == "COMPLETE")
        {
            AiInterviewState completedInterview = ToInterviewState(state, null);
            string completedText = state.IsWelfareHomeResident == true
                ? "You are marked as living in an approved welfare home. The FAS form will skip household income and household-size questions. Use 'Apply answers to form' to copy your confirmed details, then review the remaining particulars and documents before submitting."
                : "I have confirmed the details for this FAS check. Use 'Apply answers to form' to copy them into the application, or edit the form manually if anything looks wrong.";
            List<AiAction> completedActions = [new("NAVIGATE", "Open FAS application", "/portal/fas"), new("APPLY_FAS_PATCH", "Apply answers to form", Payload: completedInterview.FormPatch)];
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, completedText, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [], completedActions, completedInterview);
        }

        FasExtractionResult extraction = isNewInterview ? FasExtractionResult.Accepted() : ApplyFasAnswer(state, request.Message, fieldKey);
        if (extraction.Status == "MANUAL_FALLBACK")
        {
            state.Status = "MANUAL_FALLBACK";
            AiInterviewState manualInterview = ToInterviewState(state, null);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [],
                [new("NAVIGATE", "Open FAS application", "/portal/fas")], manualInterview);
        }

        if (extraction.Status == "CLARIFY")
        {
            state.Status = "CLARIFYING";
            AiInterviewState clarificationInterview = ToInterviewState(state, extraction.Message);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), [], [], clarificationInterview);
        }

        string? next = NextQuestion(state, fieldKey);
        object? recommendation = null;
        FasRecommendationMatch[] recommendedSchemes = [];
        string text;
        if (next is null && state.IsWelfareHomeResident == false)
        {
            try
            {
                object rawRecommendation = await fas.CheckEligibility(new EligibilityRequest(state.MonthlyHouseholdIncome!.Value,
                    state.HouseholdMemberCount!.Value, 0, state.ParentNationalities), ct);
                JsonElement root = JsonSerializer.SerializeToElement(rawRecommendation, JsonOptions);
                bool hasSchemes = root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array && schemes.GetArrayLength() > 0;
                if (!hasSchemes) { state.Status = "MANUAL_FALLBACK"; text = "Based on your details, I could not find an eligible FAS scheme. The FAS application form and Admin Center remain the official paths. Review your answers or contact Admin Center for help."; }
                else
                {
                    state.Status = "COMPLETE";
                    recommendedSchemes = ExtractRecommendationMatches(root);
                    AiInterviewState completeInterview = ToInterviewState(state, null, recommendedSchemes);
                    recommendation = BuildFasRecommendation(root, completeInterview);
                    text = "I have enough information to evaluate the active FAS schemes. Review the recommendation below and use 'Apply answers to form' when ready.";
                }
            }
            catch { state.Status = "MANUAL_FALLBACK"; text = "Based on your details, I could not find an eligible FAS scheme. The FAS application form and Admin Center remain the official paths. Review your answers or contact Admin Center for help."; }
        }
        else if (next is null)
        {
            state.Status = "COMPLETE";
            text = "I have your welfare-home status and parent or guardian nationality. The FAS form will skip household income and household-size questions. Use 'Apply answers to form', then review the remaining particulars before submitting.";
        }
        else { state.Status = "COLLECTING"; text = next; }

        AiInterviewState interview = ToInterviewState(state, next, recommendedSchemes);
        c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
        List<AiCard> cards = recommendation is null ? [] : [new("FAS_RECOMMENDATION", recommendation)];
        List<AiAction> actions = state.Status == "COMPLETE" || state.Status == "MANUAL_FALLBACK"
            ? [new("NAVIGATE", "Open FAS application", "/portal/fas")]
            : [];
        if (state.Status == "COMPLETE") actions.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: interview.FormPatch));
        return new(c.Id, 0, text, "FAS_INTERVIEW", FasInterviewGrounding(state.Status), cards, actions, interview);
    }

    private async Task<AiChatResponse> HandleGeneral(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, request.PageContext?.Domain);
        string sourceText = string.Join("\n", sources.Select(x => $"[{x.Citation.SourceId}] ({x.Citation.SourceStatus}) {x.Content}"));
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
        if (sources.Count == 0)
        {
            Guid review = await CreateReview(c, c.PersonId, "MISSING_POLICY", request.PageContext, request.Message, now, ct);
            return new(c.Id, 0, text, "FALLBACK", new(false, []), [], FallbackActions(review), null, review);
        }
        if (sources.Any(x => x.Citation.SourceStatus == "PROTOTYPE"))
        {
            text += "\n\nSome parts of this answer are based on prototype guidance and may change. Your Bills/FAS pages remain the source of truth.";
        }
        return new(c.Id, 0, text, "GENERAL", Grounding(sources), [], [], null);
    }

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

    private static string DetermineMode(string message, string current, string? domain)
    {
        string value = $"{domain} {message}".ToUpperInvariant();
        if (current == "FAS_INTERVIEW") return "FAS_INTERVIEW";
        if (IsFasInterviewRequest(value)) return "FAS_INTERVIEW";
        if (value.Contains("PAY") || value.Contains("BILL") || value.Contains("BALANCE") || value.Contains("OUTSTANDING") || value.Contains("REFUND") || value.Contains("WITHDRAW")) return "PAYMENT";
        return "GENERAL";
    }

    private static bool IsFasInterviewRequest(string value)
    {
        bool mentionsFas = value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE");
        bool asksForInterview = Regex.IsMatch(value, @"\b(APPLY|APPLICATION|CHECK|ELIGIB|QUALIF|ASSESS|START|HELP ME APPLY)\b", RegexOptions.IgnoreCase);
        bool eligibilityWithoutFas = value.Contains("ELIGIB") || value.Contains("QUALIF");
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
        return new FasInterviewData { Profile = profile, Status = "COLLECTING" };
    }
    private static FasExtractionResult ApplyFasAnswer(FasInterviewData s, string message, string? preferredField = null)
    {
        string? field = s.ClarificationField ?? NextMissingField(s, preferredField);
        if (field is null) return FasExtractionResult.Accepted();

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

        if (LooksLikeFieldHelpRequest(message))
        {
            int helpAttempts = s.ClarificationAttempts.GetValueOrDefault(field);
            if (helpAttempts >= 1)
            {
                s.ClarificationField = null;
                s.ValidationMessage = HelpForField(field);
                return FasExtractionResult.ManualFallback("I couldn't safely prefill that field. The FAS form is still the source of truth; please complete it manually.");
            }

            s.ClarificationAttempts[field] = helpAttempts + 1;
            s.ClarificationField = field;
            s.ValidationMessage = HelpForField(field);
            return FasExtractionResult.Clarify(HelpForField(field));
        }

        FasExtractionResult result = field switch
        {
            "isWelfareHomeResident" => ExtractWelfareHome(message),
            "monthlyHouseholdIncome" => ExtractIncome(message),
            "householdMemberCount" => ExtractHouseholdMemberCount(message),
            "parentNationalities" => ExtractParentNationalities(message),
            _ => FasExtractionResult.Accepted()
        };

        if (result.Status == "ACCEPTED")
        {
            s.ClarificationField = null;
            s.ValidationMessage = null;
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
    private static string? NextQuestion(FasInterviewData s, string? preferred = null)
    {
        string? field = s.ClarificationField ?? NextMissingField(s, preferred);
        return field switch
        {
            "isWelfareHomeResident" => "Are you currently residing in an approved welfare home? Please answer yes or no.",
            "monthlyHouseholdIncome" => "What is your total monthly household income in SGD?",
            "householdMemberCount" => "How many people are in your household?",
            "parentNationalities" => "What is your parent or guardian's nationality? For example: Singapore Citizen, Permanent Resident, or Foreigner.",
            _ => null
        };
    }
    private static AiInterviewState ToInterviewState(FasInterviewData s, string? next, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        List<AiInterviewField> fields =
        [
            new("isWelfareHomeResident", s.IsWelfareHomeResident, s.IsWelfareHomeResident.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.IsWelfareHomeResident.HasValue),
            new("monthlyHouseholdIncome", s.MonthlyHouseholdIncome, s.MonthlyHouseholdIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.MonthlyHouseholdIncome.HasValue),
            new("householdMemberCount", s.HouseholdMemberCount, s.HouseholdMemberCount.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.HouseholdMemberCount.HasValue),
            new("parentNationalities", s.ParentNationalities, s.ParentNationalities.Count > 0 ? "AI_CONFIRMED" : "UNMAPPED", s.ParentNationalities.Count > 0)
        ];
        string[] missing = fields.Where(x => !x.Confirmed && !(s.IsWelfareHomeResident == true && x.Name is "monthlyHouseholdIncome" or "householdMemberCount"))
            .Select(x => x.Name).ToArray();
        object? patch = s.Status == "MANUAL_FALLBACK" ? null : BuildFasFormPatch(s, recommendedSchemes);
        return new(s.Status, next, fields, missing, patch);
    }

    private static FasFormPatch BuildFasFormPatch(FasInterviewData s, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        var particulars = new FasPatchParticulars(
            Email: TryGetString(s.Profile, "email"),
            ParentNationalities: s.ParentNationalities.Count > 0 ? s.ParentNationalities.ToArray() : null);
        var income = new FasPatchIncome(
            s.IsWelfareHomeResident,
            TryGetString(s.Profile, "employmentStatusCode") ?? "EMPLOYED",
            s.MonthlyHouseholdIncome,
            s.HouseholdMemberCount,
            TryGetDecimal(s.Profile, "otherMonthlyIncome"));
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
            AddMeta("employmentStatusCode", TryGetString(s.Profile, "employmentStatusCode"), "PROFILE", "From your profile.");
            AddMeta("monthlyHouseholdIncome", s.MonthlyHouseholdIncome, s.MonthlyHouseholdIncome.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
                s.MonthlyHouseholdIncome.HasValue ? $"You said your household income is ${s.MonthlyHouseholdIncome.Value:N0}." : null);
            AddMeta("householdMemberCount", s.HouseholdMemberCount, s.HouseholdMemberCount.HasValue ? "AI_CONFIRMED" : "UNMAPPED",
                s.HouseholdMemberCount.HasValue ? $"You said there are {s.HouseholdMemberCount.Value} household members." : null);
            AddMeta("otherMonthlyIncome", TryGetDecimal(s.Profile, "otherMonthlyIncome"), "PROFILE", "From your profile.");
        }
        AddMeta("parentNationalities", s.ParentNationalities.Count > 0 ? s.ParentNationalities : null,
            s.ParentNationalities.Count > 0 ? "AI_CONFIRMED" : "UNMAPPED",
            s.ParentNationalities.Count > 0 ? $"Nationalit{(s.ParentNationalities.Count == 1 ? "y" : "ies")}: {string.Join(", ", s.ParentNationalities)}." : null);
        FasPatchSchemes? schemes = recommendedSchemes is { Count: > 0 }
            ? new FasPatchSchemes(recommendedSchemes.Select(x => x.SchemeId).Distinct().ToArray(),
                recommendedSchemes.Select(x => x.SchemeName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
            : null;
        return new FasFormPatch(particulars, income, schemes, meta);
    }
    private static string? NextMissingField(FasInterviewData s, string? preferred = null)
    {
        if (preferred is not null)
        {
            if (preferred.Equals("isWelfareHomeResident", StringComparison.OrdinalIgnoreCase) && !s.IsWelfareHomeResident.HasValue) return "isWelfareHomeResident";
            if (preferred.Equals("monthlyHouseholdIncome", StringComparison.OrdinalIgnoreCase) && s.IsWelfareHomeResident == false && !s.MonthlyHouseholdIncome.HasValue) return "monthlyHouseholdIncome";
            if (preferred.Equals("householdMemberCount", StringComparison.OrdinalIgnoreCase) && s.IsWelfareHomeResident == false && (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0)) return "householdMemberCount";
            if (preferred.Equals("parentNationalities", StringComparison.OrdinalIgnoreCase) && s.ParentNationalities.Count == 0) return "parentNationalities";
        }
        if (!s.IsWelfareHomeResident.HasValue) return "isWelfareHomeResident";
        if (!s.IsWelfareHomeResident.Value)
        {
            if (!s.MonthlyHouseholdIncome.HasValue) return "monthlyHouseholdIncome";
            if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0) return "householdMemberCount";
        }
        if (s.ParentNationalities.Count == 0) return "parentNationalities";
        return null;
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
                }
                break;
            case "monthlyHouseholdIncome":
                s.MonthlyHouseholdIncome = (decimal)value!;
                break;
            case "householdMemberCount":
                s.HouseholdMemberCount = (int)value!;
                break;
            case "parentNationalities":
                s.ParentNationalities = ((IReadOnlyCollection<string>)value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                break;
        }
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
        return Regex.IsMatch(message, @"\b(what are|what is|options|option|choose|choices|example|examples|not sure|don't know|do not know|idk|help)\b", RegexOptions.IgnoreCase);
    }

    private static string HelpForField(string field) => field switch
    {
        "isWelfareHomeResident" => "An approved welfare home is a formally recognised residential home. Reply yes or no: yes if you live in one, otherwise no.",
        "monthlyHouseholdIncome" => "Use the total monthly income for everyone in your household, in SGD. Example: 3500.",
        "householdMemberCount" => "Count everyone in your household, including yourself. Reply with one whole number, for example 4.",
        "parentNationalities" => "Common options are Singapore Citizen, Permanent Resident, Foreigner, or the specific nationality/country on their records. Reply with the closest value.",
        _ => "Tell me the value shown on your records, or leave it for manual entry on the form."
    };

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
            return FasExtractionResult.Clarify("Please provide the parent or guardian nationality as text, for example Singapore Citizen.");

        string[] values = Regex.Split(normalized, @"\s*(?:,|/|\band\b|&)\s*", RegexOptions.IgnoreCase)
            .Select(NormalizeNationality)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return values.Length == 0
            ? FasExtractionResult.Clarify("Please provide the parent or guardian nationality as text, for example Singapore Citizen.")
            : FasExtractionResult.Accepted(values);
    }

    private static IEnumerable<decimal> ExtractNumbers(string message)
    {
        foreach (Match match in Regex.Matches(message, @"(?<![\w.-])-?\d[\d,]*(?:\.\d+)?\s*[kK]?", RegexOptions.CultureInvariant))
        {
            string raw = match.Value.Trim();
            bool thousand = raw.EndsWith("k", StringComparison.OrdinalIgnoreCase);
            raw = raw.TrimEnd('k', 'K').Replace(",", string.Empty);
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                yield return thousand ? value * 1000 : value;
        }
    }

    private static string NormalizeNationality(string value)
    {
        string trimmed = value.Trim().Trim('.');
        return trimmed.ToUpperInvariant() switch
        {
            "SG" or "SINGAPORE" or "SINGAPOREAN" or "SINGAPORE CITIZEN" => "Singapore Citizen",
            _ => CultureInfo.GetCultureInfo("en-SG").TextInfo.ToTitleCase(trimmed.ToLowerInvariant())
        };
    }

    private static FasRecommendationCard BuildFasRecommendation(object rawRecommendation, AiInterviewState interview)
    {
        JsonElement root = JsonSerializer.SerializeToElement(rawRecommendation, JsonOptions);
        decimal? pci = TryGetDecimal(root, "perCapitaIncome");
        FasRecommendationMatch[] matches = ExtractRecommendationMatches(root);
        FasRecommendationMatch? recommended = matches.FirstOrDefault();
        return new FasRecommendationCard(
            pci,
            recommended?.SchemeName,
            recommended?.TierLabel,
            recommended?.SubsidyType,
            recommended?.SubsidyValue,
            matches,
            interview.Fields.Where(x => x.Confirmed).ToArray(),
            interview.MissingFields,
            "Prototype recommendation. Eligibility is calculated by application code and final approval remains subject to MOE review.");
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
        return schemeId.HasValue && tierId.HasValue && schemeName is not null && tierLabel is not null && subsidyType is not null && subsidyValue.HasValue
            ? new FasRecommendationMatch(schemeId.Value, schemeName, tierId.Value, tierLabel, subsidyType, subsidyValue.Value)
            : null;
    }

    private static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? TryGetInt64(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.TryGetInt64(out long result) ? result : null;
    private static decimal? TryGetDecimal(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.TryGetDecimal(out decimal result) ? result : null;
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

    private static string SerializeResponse(AiChatResponse response)
    {
        var data = new
        {
            response.Mode,
            response.Cards,
            response.Actions,
            response.ReviewRecordId,
            response.InterviewState,
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
        public decimal? MonthlyHouseholdIncome { get; set; }
        public int? HouseholdMemberCount { get; set; }
        public List<string> ParentNationalities { get; set; } = [];
        public string? ClarificationField { get; set; }
        public string? ValidationMessage { get; set; }
        public Dictionary<string, int> ClarificationAttempts { get; set; } = [];
    }

    private sealed record FasExtractionResult(string Status, object? Value = null, string? Message = null)
    {
        public static FasExtractionResult Accepted(object? value = null) => new("ACCEPTED", value);
        public static FasExtractionResult Clarify(string message) => new("CLARIFY", Message: message);
        public static FasExtractionResult ManualFallback(string message) => new("MANUAL_FALLBACK", Message: message);
    }
}
