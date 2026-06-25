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
            const string text = "I could not complete that request reliably. You can continue in the portal, review the help links, or contact the Admin Center.";
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
        if (intent.Contains("HISTORY") || intent.Contains("PAID") || intent.Contains("REFUND"))
        {
            string historyText = snapshot.RecentPayments.Count == 0
                ? "I could not find recent payment records for your account yet. Your Bills & payments page will still show any current outstanding charges. Refunds depend on the payment type and conditions of the original transaction."
                : $"I found {snapshot.RecentPayments.Count} recent payment record(s) for you. Refunds are processed based on the original payment method and may take 5-14 business days. Contact your school or Admin Center for refund eligibility.";
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
                ? "I do not see any outstanding course bills right now. Your Bills & payments page will still show historical bills and payments."
                : $"I found {snapshot.BillCount} outstanding course bill(s) recorded by MOE, totalling {snapshot.TotalOutstanding.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.";
            return new(c.Id, 0, billText, "PAYMENT", Grounding(sources),
                [new("OUTSTANDING_BILLS", snapshot.Bills)], [new("NAVIGATE", "Open Bills & payments page", "/portal/bills")], null);
        }
        string paymentOptions = PaymentOptionsText(snapshot);
        string warning = snapshot.TotalOutstanding > 0
            ? $" {paymentOptions}"
            : " You have no outstanding charges.";
        string text = $"Here is what I found for your Education Account: {snapshot.AvailableBalance.ToString("C", CultureInfo.GetCultureInfo("en-SG"))} is available, current outstanding charges total {snapshot.TotalOutstanding.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}, and your net available amount is {snapshot.NetAvailable.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.{warning}";
        AiCard card = new("FINANCE_SUMMARY", snapshot);
        AiAction[] actions = [new("NAVIGATE", "Open Bills & payments page", "/portal/bills"), new("NAVIGATE", "Open education account", "/portal/account")];
        return new(c.Id, 0, text, "PAYMENT", Grounding(sources), [card], actions, null);
    }

    private async Task<AiChatResponse> HandleFas(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        bool isNewInterview = c.FasInterviewJson is null;
        FasInterviewData state;
        try { state = DeserializeState(c.FasInterviewJson) ?? await InitializeFasState(ct); }
        catch { return new(c.Id, 0, "I could not retrieve your profile information right now. Please proceed with the FAS form directly or contact Admin Center if the issue persists.", "FALLBACK", new(false, []), [], [new("NAVIGATE", "Open FAS application", "/portal/fas")], null); }
        FasExtractionResult extraction = isNewInterview ? FasExtractionResult.Accepted() : ApplyFasAnswer(state, request.Message);
        if (extraction.Status == "MANUAL_FALLBACK")
        {
            state.Status = "MANUAL_FALLBACK";
            AiInterviewState manualInterview = ToInterviewState(state, null);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", Grounding(knowledge.Retrieve(request.Message, "FAS")), [],
                [new("NAVIGATE", "Open FAS application", "/portal/fas")], manualInterview);
        }

        if (extraction.Status == "CLARIFY")
        {
            state.Status = "CLARIFYING";
            AiInterviewState clarificationInterview = ToInterviewState(state, extraction.Message);
            c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
            return new(c.Id, 0, extraction.Message!, "FAS_INTERVIEW", Grounding(knowledge.Retrieve(request.Message, "FAS")), [],
                [new("NAVIGATE", "Open FAS application", "/portal/fas")], clarificationInterview);
        }

        string? next = NextQuestion(state);
        object? recommendation = null;
        string text;
        if (next is null && state.IsWelfareHomeResident == false)
        {
            try
            {
                object rawRecommendation = await fas.CheckEligibility(new EligibilityRequest(state.MonthlyHouseholdIncome!.Value,
                    state.HouseholdMemberCount!.Value, 0, state.ParentNationalities), ct);
                JsonElement root = JsonSerializer.SerializeToElement(rawRecommendation, JsonOptions);
                bool hasSchemes = root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array && schemes.GetArrayLength() > 0;
                if (!hasSchemes) { state.Status = "MANUAL_FALLBACK"; text = "Based on your information, I could not find a matching FAS scheme. Please proceed with the FAS form to verify manually or contact Admin Center for assistance."; }
                else { state.Status = "COMPLETE"; AiInterviewState completeInterview = ToInterviewState(state, null); recommendation = BuildFasRecommendation(rawRecommendation, completeInterview); text = "I have enough confirmed information to evaluate the active FAS schemes. Review the recommendation and apply the confirmed answers to the form when ready."; }
            }
            catch { state.Status = "MANUAL_FALLBACK"; text = "I could not complete the eligibility check right now. Please proceed with the FAS form or contact Admin Center for assistance."; }
        }
        else if (next is null)
        {
            state.Status = "COMPLETE";
            text = "Your welfare-home status is confirmed. The application form will skip income fields and continue with the required supporting information.";
        }
        else { state.Status = "COLLECTING"; text = next; }

        AiInterviewState interview = ToInterviewState(state, next);
        c.Touch("FAS_INTERVIEW", request.PageContext is null ? null : JsonSerializer.Serialize(request.PageContext, JsonOptions), JsonSerializer.Serialize(state, JsonOptions), now);
        List<AiCard> cards = recommendation is null ? [] : [new("FAS_RECOMMENDATION", recommendation)];
        List<AiAction> actions = [new("NAVIGATE", "Open FAS application", "/portal/fas")];
        if (state.Status == "COMPLETE") actions.Add(new("APPLY_FAS_PATCH", "Apply answers to form", Payload: interview.FormPatch));
        return new(c.Id, 0, text, "FAS_INTERVIEW", Grounding(knowledge.Retrieve(request.Message, "FAS")), cards, actions, interview);
    }

    private async Task<AiChatResponse> HandleGeneral(AiConversation c, AiChatRequest request, DateTime now, CancellationToken ct)
    {
        IReadOnlyList<KnowledgeResult> sources = knowledge.Retrieve(request.Message, request.PageContext?.Domain);
        var history = new ChatHistory($"""
            You are the MOE Student Finance Copilot. Use MOE records and policy documents to answer. Your answers may be incomplete — direct users to portal pages as their source of truth.
            Never invent personal data, policy, eligibility, amounts, status, or timelines. Label PROTOTYPE sources.
            If sources are insufficient, say so and direct the user to Admin Center. Cite source IDs in square brackets.
            Sources:
            {string.Join("\n", sources.Select(x => $"[{x.Citation.SourceId}] ({x.Citation.SourceStatus}) {x.Content}"))}
            """);
        history.AddUserMessage(request.Message);
        Kernel kernel = services.GetRequiredService<Kernel>();
        ChatMessageContent answer = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(history, kernel: kernel, cancellationToken: ct);
        string text = string.IsNullOrWhiteSpace(answer.Content) ? "I do not have enough reliable information to answer that." : answer.Content.Trim();
        if (sources.Count == 0)
        {
            Guid review = await CreateReview(c, c.PersonId, "MISSING_POLICY", request.PageContext, request.Message, now, ct);
            return new(c.Id, 0, text, "FALLBACK", new(false, []), [], FallbackActions(review), null, review);
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
        if (current == "FAS_INTERVIEW" || value.Contains("FAS") || value.Contains("FINANCIAL ASSISTANCE") || value.Contains("ELIGIB")) return "FAS_INTERVIEW";
        if (value.Contains("PAY") || value.Contains("BILL") || value.Contains("BALANCE") || value.Contains("OUTSTANDING") || value.Contains("REFUND") || value.Contains("WITHDRAW")) return "PAYMENT";
        return "GENERAL";
    }

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

        return new AiPageContext(domain, surface, path, null);
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
    private static FasExtractionResult ApplyFasAnswer(FasInterviewData s, string message)
    {
        string? field = s.ClarificationField ?? NextMissingField(s);
        if (field is null) return FasExtractionResult.Accepted();

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
            return FasExtractionResult.ManualFallback("I could not confirm that answer safely. Please continue in the FAS form; your manual entries remain available and editable.");
        }

        s.ClarificationAttempts[field] = attempts + 1;
        s.ClarificationField = field;
        s.ValidationMessage = result.Message;
        return result;
    }
    private static string? NextQuestion(FasInterviewData s)
    {
        string? field = s.ClarificationField ?? NextMissingField(s);
        return field switch
        {
            "isWelfareHomeResident" => "Are you currently residing in an approved welfare home? Please answer yes or no.",
            "monthlyHouseholdIncome" => "What is your total monthly household income in SGD?",
            "householdMemberCount" => "How many people are in your household?",
            "parentNationalities" => "What is your parent or guardian's nationality?",
            _ => null
        };
    }
    private static AiInterviewState ToInterviewState(FasInterviewData s, string? next)
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
        object? patch = s.Status == "MANUAL_FALLBACK"
            ? null
            : new FasFormPatch(s.IsWelfareHomeResident, s.MonthlyHouseholdIncome, s.HouseholdMemberCount, 0, s.ParentNationalities,
                Provenance: fields.ToDictionary(x => x.Name, x => x.Provenance, StringComparer.OrdinalIgnoreCase));
        return new(s.Status, next, fields, missing, patch);
    }
    private static string? NextMissingField(FasInterviewData s)
    {
        if (!s.IsWelfareHomeResident.HasValue) return "isWelfareHomeResident";
        if (s.IsWelfareHomeResident.Value) return null;
        if (!s.MonthlyHouseholdIncome.HasValue) return "monthlyHouseholdIncome";
        if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0) return "householdMemberCount";
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

        bool notNegatesWelfare = Regex.IsMatch(value, @"\bnot\b.{0,20}\b(welfare|approved)\b", RegexOptions.IgnoreCase);
        if (notNegatesWelfare) return FasExtractionResult.Accepted(false);

        bool yes = Regex.IsMatch(value, @"\b(yes|y|welfare home|approved welfare)\b", RegexOptions.IgnoreCase);
        bool no = Regex.IsMatch(value, @"\b(no|n|not|do not|don't)\b", RegexOptions.IgnoreCase);

        if (yes && !no) return FasExtractionResult.Accepted(true);
        if (no && !yes) return FasExtractionResult.Accepted(false);
        return FasExtractionResult.Clarify("Please confirm welfare-home status with yes or no.");
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
        FasRecommendationMatch[] matches = root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array
            ? schemes.EnumerateArray().Select(ToRecommendationMatch).Where(x => x is not null).Cast<FasRecommendationMatch>().ToArray()
            : [];
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
    private static AiAction[] FallbackActions(Guid review) =>
    [
        new("NAVIGATE", "Education Account FAQ", "/portal/account"),
        new("NAVIGATE", "Payment FAQ", "/portal/bills"),
        new("NAVIGATE", "FAS FAQ", "/portal/fas"),
        new("CONTACT_ADMIN_CENTER", "Contact Admin Center", Payload: new { reviewRecordId = review })
    ];

    private static string PaymentOptionsText(AiFinanceSnapshot snapshot)
    {
        if (snapshot.TotalOutstanding <= 0m) return "There is nothing due right now.";
        string settle = " Consider settling your outstanding charges before proceeding with new course enrolments or withdrawals.";
        if (snapshot.AvailableBalance >= snapshot.TotalOutstanding)
            return $"Your Education Account balance covers the outstanding amount; review the bill details before paying.{settle}";
        if (snapshot.AvailableBalance > 0m)
        {
            decimal remainder = snapshot.TotalOutstanding - snapshot.AvailableBalance;
            return $"Your Education Account can cover part of the outstanding amount, but is short by {remainder.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}. You may use split payment or another available online payment method for the remainder when the bill supports it.{settle}";
        }
        return $"Your Education Account does not have available funds to cover the outstanding amount. Use another available online payment method when the bill supports it.{settle}";
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
