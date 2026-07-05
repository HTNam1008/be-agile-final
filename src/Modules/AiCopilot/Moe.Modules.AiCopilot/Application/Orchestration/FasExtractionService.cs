using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FasExtractionService(Kernel kernel)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string AllExtractionSchema = """
    {
      "type": "object",
      "properties": {
        "monthlyHouseholdIncome": { "type": ["number", "null"] },
        "householdMemberCount": { "type": ["integer", "null"] },
        "isWelfareHomeResident": { "type": ["boolean", "null"] },
        "parentNationalities": { "type": ["array", "null"], "items": { "type": "string" } },
        "employmentStatusCode": { "type": ["string", "null"], "enum": ["EMPLOYED", "SELF_EMPLOYED", "UNEMPLOYED"] },
        "otherMonthlyIncome": { "type": ["number", "null"] },
        "email": { "type": ["string", "null"] },
        "ambiguous": { "type": "boolean" },
        "ambiguityReason": { "type": ["string", "null"] }
      },
      "required": ["ambiguous"]
    }
    """;

    private static readonly JsonElement AllExtractionSchemaElement;

    static FasExtractionService()
    {
        using var doc = JsonDocument.Parse(AllExtractionSchema);
        AllExtractionSchemaElement = doc.RootElement.Clone();
    }

    private static PromptExecutionSettings CreateJsonSchemaExecutionSettings(JsonElement schemaElement, string schemaName, bool strict = true)
    {
        return new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                ["response_format"] = new Dictionary<string, object>
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new Dictionary<string, object>
                    {
                        ["name"] = schemaName,
                        ["strict"] = strict,
                        ["schema"] = schemaElement
                    }
                }
            }
        };
    }

    public async Task<FasExtractionResult> ApplyFasAnswerWithLlmAsync(FasInterviewData s, string message, Func<string?, string?> resolveTargetField, string? preferredField = null, CancellationToken ct = default)
    {
        string? field = resolveTargetField(preferredField);
        if (field is null) return FasExtractionResult.Accepted();

        FasInterviewData preSnapshot = Snapshot(s);
        FasExtractionResult? allResult = await TryLlmExtractAllAsync(s, message, field, ct);
        if (allResult?.Status == "ACCEPTED")
            return allResult;

        Restore(s, preSnapshot);

        FasExtractionResult? llmResult = await TryLlmExtractFieldAsync(message, field, ct);
        if (llmResult is not null)
        {
            if (llmResult.Status == "ACCEPTED")
                ApplyExtraction(s, field, llmResult.Value);
            return llmResult;
        }

        return ApplyFasAnswer(s, message, resolveTargetField, preferredField);
    }

    private async Task<FasExtractionResult?> TryLlmExtractAllAsync(FasInterviewData s, string message, string expectedField, CancellationToken ct)
    {
        if (LooksLikeFieldHelpRequest(message))
            return null;

        try
        {
            IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();
            string fieldsNeeded = string.Join(", ", NextAllMissingFields(s));
            string fieldContext = HelpForField(expectedField);

            var history = new ChatHistory($$"""
You are helping a student fill out a Singapore FAS application.
The student is answering: "{{fieldContext}}"

Their answer may cover one or more needed fields: {{fieldsNeeded}}

Rules:
- "k"/"K" -> multiply by 1000
- Always try to extract a number. Set ambiguous=false and extract the best value you can find.
- Multiple numbers ("3000 4000") -> add them together to get total. Example: "3000 4000" -> monthlyHouseholdIncome=7000, ambiguous=false.
- Multiple incomes in a story ("father earns 2500 and mother earns 1800") -> add them together. Example: "father earns about 2500 and mother earns 1800, combined it's around there" -> monthlyHouseholdIncome=4300, ambiguous=false.
- "combined", "total", "together" -> add the mentioned amounts, ambiguous=false.
- Range with clear bounds ("between 3000 and 4000", "3k to 4k") -> use the lower bound, ambiguous=false.
- "none"/"nil"/"zero" for otherMonthlyIncome -> 0
- "about"/"around"/"approximately" -> extract the number, ambiguous=false.
- "yes"/"yeah"/"y"/"correct" for isWelfareHomeResident -> true
- "no"/"n"/"nah" for isWelfareHomeResident -> false
- parentNationalities: standard demonyms (Singaporean, Malaysian, Chinese, Indian, etc.)
- employmentStatusCode: "working"->EMPLOYED, "no job"->UNEMPLOYED, "freelance"->SELF_EMPLOYED

CRITICAL: Set ambiguous=true ONLY when the user explicitly says they don't know or can't provide a number ("I'm not sure", "I don't know", "maybe 3k or 4k" with doubt). If the user provides ANY specific amounts (even as a range or multiple numbers), set ambiguous=false and extract the best value.
""");
            history.AddUserMessage(message);

            var settings = CreateJsonSchemaExecutionSettings(AllExtractionSchemaElement, "fas_extraction");
            ChatMessageContent result = await chat.GetChatMessageContentAsync(history, executionSettings: settings, kernel: kernel, cancellationToken: ct);
            string content = result.Content?.Trim() ?? "";
            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;
            var (extResult, changes) = ValidateExtraction(root, s, expectedField);
            if (extResult.Status == "ACCEPTED")
            {
                foreach (var (field, value) in changes)
                    ApplyAcceptedValue(s, field, value);
                s.ClarificationField = null;
                s.ValidationMessage = null;
                s.PendingParentNationalitySuggestion = null;
                foreach (var (field, _) in changes)
                    s.ClarificationAttempts.Remove(field);
            }
            return extResult;
        }
        catch { return null; }
    }

    private static (FasExtractionResult Result, List<(string Field, object? Value)> Changes) ValidateExtraction(JsonElement root, FasInterviewData s, string target)
    {
        bool ambiguous = root.TryGetProperty("ambiguous", out JsonElement ambEl) && ambEl.ValueKind == JsonValueKind.True;

        var changes = new List<(string Field, object? Value)>();

        if (TryGetJsonBool(root, "isWelfareHomeResident") is bool welfare)
        {
            if (s.IsWelfareHomeResident.HasValue && s.IsWelfareHomeResident.Value != welfare)
                return (FasExtractionResult.Clarify(welfare
                    ? "You previously said you are NOT a welfare home resident. Do you want to change that to YES, you live in an approved welfare home?"
                    : "You previously said you ARE a welfare home resident. Do you want to change that to NO, you don't live in a welfare home?"), changes);
            if (!s.IsWelfareHomeResident.HasValue || s.IsWelfareHomeResident.Value != welfare)
            {
                changes.Add(("isWelfareHomeResident", welfare));
            }
        }

        if (TryGetJsonString(root, "email") is string email && email.Contains('@'))
        {
            changes.Add(("email", email));
        }

        if (TryGetJsonString(root, "employmentStatusCode") is string emp && emp is "EMPLOYED" or "SELF_EMPLOYED" or "UNEMPLOYED")
        {
            changes.Add(("employmentStatusCode", emp));
        }

        if (TryGetJsonDecimal(root, "monthlyHouseholdIncome") is decimal income)
        {
            if (income < 0 || income > 100_000)
                return (ambiguous
                    ? FasExtractionResult.Clarify($"I think the household income is around ${income:N0}, but that seems high. Could you confirm the exact monthly household income in SGD?")
                    : FasExtractionResult.Clarify($"${income:N0} seems high for monthly household income. Could you check and provide the correct amount?"), changes);
            if (!s.MonthlyHouseholdIncome.HasValue || s.MonthlyHouseholdIncome != income)
            {
                changes.Add(("monthlyHouseholdIncome", decimal.Round(income, 2)));
            }
        }

        if (TryGetJsonInt(root, "householdMemberCount") is int count)
        {
            if (count < 1 || count > 30)
                return (FasExtractionResult.Clarify($"A household of {count} people seems unusual. Could you confirm the number of household members?"), changes);
            if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount != count)
            {
                changes.Add(("householdMemberCount", count));
            }
        }

        if (TryGetJsonDecimal(root, "otherMonthlyIncome") is decimal otherInc)
        {
            if (otherInc < 0 || otherInc > 100_000)
                return (FasExtractionResult.Clarify($"The other monthly income of ${otherInc:N0} seems high. Could you confirm?"), changes);
            if (!s.OtherMonthlyIncome.HasValue || s.OtherMonthlyIncome != otherInc)
            {
                changes.Add(("otherMonthlyIncome", decimal.Round(otherInc, 2)));
            }
        }

        if (root.TryGetProperty("parentNationalities", out JsonElement natEl) && natEl.ValueKind == JsonValueKind.Array)
        {
            var nats = new List<string>();
            foreach (JsonElement item in natEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string raw = item.GetString()!;
                    string? norm = TryNormalizeParentNationality(raw) ?? TryMapCountryToParentNationalitySuggestion(raw);
                    if (norm is not null) nats.Add(norm);
                }
            }
            if (nats.Count > 0)
            {
                changes.Add(("parentNationalities", nats.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
            }
        }

        if (changes.Count == 0 && ambiguous)
            return (FasExtractionResult.Clarify(root.TryGetProperty("ambiguityReason", out JsonElement reason2El)
                ? reason2El.GetString() ?? "Could you clarify your answer for the FAS application?"
                : "Could you clarify your answer for the FAS application?"), changes);

        if (changes.Count > 0)
            return (FasExtractionResult.Accepted(), changes);

        return (FasExtractionResult.Clarify("Let me check — could you tell me more so I can fill in the next field for the FAS application?"), changes);
    }

    private static IEnumerable<string> NextAllMissingFields(FasInterviewData s)
    {
        if (!s.IsWelfareHomeResident.HasValue) yield return "isWelfareHomeResident";
        if (s.IsWelfareHomeResident != true)
        {
            if (!s.MonthlyHouseholdIncome.HasValue) yield return "monthlyHouseholdIncome";
            if (!s.HouseholdMemberCount.HasValue || s.HouseholdMemberCount <= 0) yield return "householdMemberCount";
            if (!s.OtherMonthlyIncome.HasValue) yield return "otherMonthlyIncome";
        }
        if (s.ParentNationalities.Count == 0) yield return "parentNationalities";
    }

    private static bool? TryGetJsonBool(JsonElement root, string key)
    {
        if (root.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.True) return true;
        if (root.TryGetProperty(key, out el) && el.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    private static string? TryGetJsonString(JsonElement root, string key)
    {
        if (root.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.String)
            return el.GetString();
        return null;
    }

    private static decimal? TryGetJsonDecimal(JsonElement root, string key)
    {
        if (root.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out decimal v))
            return v;
        return null;
    }

    private static int? TryGetJsonInt(JsonElement root, string key)
    {
        if (root.TryGetProperty(key, out JsonElement el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int v))
            return v;
        return null;
    }

    private async Task<FasExtractionResult?> TryLlmExtractFieldAsync(string message, string field, CancellationToken ct)
    {
        if (field is not "monthlyHouseholdIncome" and not "householdMemberCount" and not "otherMonthlyIncome")
            return null;

        try
        {
            IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();

            string fieldSchema = $$"""
            {
              "type": "object",
              "properties": {
                "value": { "type": ["number", "null"] },
                "field": { "type": "string", "enum": ["{{field}}"] },
                "ambiguous": { "type": "boolean" }
              },
              "required": ["value", "field", "ambiguous"]
            }
            """;
            using var schemaDoc = JsonDocument.Parse(fieldSchema);
            var settings = CreateJsonSchemaExecutionSettings(schemaDoc.RootElement.Clone(), "fas_field_extraction", strict: false);

            var history = new ChatHistory($$"""
You extract student FAS application data from a Singapore user message.
Field to extract: "{{field}}"

Rules:
- For monthlyHouseholdIncome: multiple amounts ("mum earns 2k, dad earns 3k", "3000 4000") -> add them together, ambiguous=false.
- For monthlyHouseholdIncome: "combined", "total", "together" -> add the amounts, ambiguous=false.
- For monthlyHouseholdIncome: range ("between 3k and 4k", "3k to 4k") -> use lower bound, ambiguous=false.
- For householdMemberCount: must be an integer.
- For otherMonthlyIncome: "none", "no", "nothing", "nil", "zero" → 0.
- "about", "around", "approximately" -> extract the number, ambiguous=false.
- Number with "k" or "K" -> multiply by 1000.
- field name is literal, do not guess.
- Set value to null ONLY if the user message contains no information for this field.
- CRITICAL: Set ambiguous=true ONLY when user says they don't know (e.g. "I'm not sure", "I don't know"). If user gives ANY specific amounts, set ambiguous=false.
""");
            history.AddUserMessage(message);

            ChatMessageContent result = await chat.GetChatMessageContentAsync(history, executionSettings: settings, kernel: kernel, cancellationToken: ct);
            string content = result.Content?.Trim() ?? "";
            using JsonDocument doc = JsonDocument.Parse(content);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("field", out JsonElement fieldEl) || fieldEl.GetString() != field)
                return null;

            if (!root.TryGetProperty("value", out JsonElement valueEl) || valueEl.ValueKind == JsonValueKind.Null)
                return null;

            return field switch
            {
                "monthlyHouseholdIncome" => ParseLlmIncome(valueEl),
                "householdMemberCount" => ParseLlmHouseholdCount(valueEl),
                "otherMonthlyIncome" => ParseLlmIncome(valueEl),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static FasExtractionResult ParseLlmIncome(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out decimal amount))
            return FasExtractionResult.Clarify("I could not understand the income figure. Please provide the amount as a number.");
        if (amount < 0 || amount > 1_000_000)
            return FasExtractionResult.Clarify("Please provide a valid non-negative monthly household income in SGD.");
        return FasExtractionResult.Accepted(decimal.Round(amount, 2));
    }

    private static FasExtractionResult ParseLlmHouseholdCount(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int count))
            return FasExtractionResult.Clarify("I could not understand the household size. Please provide the number of people as a whole number.");
        if (count is < 1 or > 30)
            return FasExtractionResult.Clarify("Please provide a household member count between 1 and 30.");
        return FasExtractionResult.Accepted(count);
    }

    internal static FasExtractionResult ApplyFasAnswer(FasInterviewData s, string message, Func<string?, string?> resolveTargetField, string? preferredField = null)
    {
        string? field = resolveTargetField(preferredField);
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
                    ApplyExtraction(s, field, new[] { suggestion });
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
                s.ClarificationAttempts.Remove(field);
                s.ClarificationAttempts.Remove("isWelfareHomeResident");
                ApplyAcceptedValue(s, "isWelfareHomeResident", welfareCorrection.Value);
                s.ClarificationField = null;
                s.ValidationMessage = null;
                return welfareCorrection;
            }
        }

        if (field != "email" && LooksLikeFieldHelpRequest(message))
        {
            if (LooksLikeUncertaintyAnswer(message))
            {
                int helpAttempts = s.HelpAttempts.GetValueOrDefault(field);
                if (helpAttempts >= 1)
                {
                    s.ClarificationField = null;
                    s.ValidationMessage = HelpForField(field);
                    return FasExtractionResult.ManualFallback("I couldn't safely prefill that field. The FAS form is still the source of truth; please complete it manually.");
                }

                s.HelpAttempts[field] = helpAttempts + 1;
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
            ApplyExtraction(s, field, result.Value);
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

    internal static FasExtractionResult ExtractWelfareHome(string message)
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

    internal static FasExtractionResult ExtractConfirmation(string message)
    {
        string value = message.Trim();
        bool yes = Regex.IsMatch(value, @"\b(yes|y|correct|confirm|confirmed|looks right|that's right|that is right)\b", RegexOptions.IgnoreCase);
        bool no = Regex.IsMatch(value, @"\b(no|n|wrong|incorrect|edit|change|not correct|not right)\b", RegexOptions.IgnoreCase);
        if (yes && !no) return FasExtractionResult.Accepted(true);
        if (no && !yes) return FasExtractionResult.Accepted(false);
        return FasExtractionResult.Clarify("Please reply yes if these details are correct, or no if you want to stop and edit the form manually.");
    }

    internal static FasExtractionResult ExtractIncome(string message)
    {
        decimal[] numbers = ExtractNumbers(message).ToArray();
        if (numbers.Length == 0) return FasExtractionResult.Clarify("Please provide your total monthly household income as an SGD amount, for example 3200.");
        if (numbers.Length > 1) return FasExtractionResult.Clarify("I found more than one amount. Please reply with only the total monthly household income in SGD.");
        decimal income = numbers[0];
        if (income < 0 || income > 1_000_000) return FasExtractionResult.Clarify("Please provide a valid non-negative monthly household income in SGD.");
        return FasExtractionResult.Accepted(decimal.Round(income, 2));
    }

    internal static FasExtractionResult ExtractHouseholdMemberCount(string message)
    {
        decimal[] numbers = ExtractNumbers(message).ToArray();
        if (numbers.Length == 0) return FasExtractionResult.Clarify("Please provide the number of people in your household, for example 4.");
        if (numbers.Length > 1 || numbers[0] != decimal.Truncate(numbers[0])) return FasExtractionResult.Clarify("Please reply with one whole number for household members.");
        int count = (int)numbers[0];
        if (count is < 1 or > 30) return FasExtractionResult.Clarify("Please provide a household member count between 1 and 30.");
        return FasExtractionResult.Accepted(count);
    }

    internal static FasExtractionResult ExtractOtherIncome(string message)
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

    internal static FasExtractionResult ExtractParentNationalities(string message)
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

    internal static IEnumerable<decimal> ExtractNumbers(string message)
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

    internal static string? TryNormalizeParentNationality(string value)
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

    internal static void ApplyAcceptedValue(FasInterviewData s, string field, object? value)
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
            case "email": s.Email = (string)value!; break;
            case "employmentStatusCode": s.EmploymentStatusCode = (string)value!; break;
            case "monthlyHouseholdIncome": s.MonthlyHouseholdIncome = (decimal)value!; break;
            case "householdMemberCount": s.HouseholdMemberCount = (int)value!; break;
            case "otherMonthlyIncome": s.OtherMonthlyIncome = (decimal)value!; break;
            case "parentNationalities":
                s.ParentNationalities = ((IReadOnlyCollection<string>)value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                break;
        }
    }

    private static void ApplyExtraction(FasInterviewData s, string field, object? value)
    {
        ApplyAcceptedValue(s, field, value);
        s.ClarificationField = null;
        s.ValidationMessage = null;
        s.PendingParentNationalitySuggestion = null;
        s.ClarificationAttempts.Remove(field);
    }

    private static FasInterviewData Snapshot(FasInterviewData s) => new()
    {
        Status = s.Status,
        Profile = s.Profile,
        IsWelfareHomeResident = s.IsWelfareHomeResident,
        Email = s.Email,
        EmploymentStatusCode = s.EmploymentStatusCode,
        MonthlyHouseholdIncome = s.MonthlyHouseholdIncome,
        HouseholdMemberCount = s.HouseholdMemberCount,
        OtherMonthlyIncome = s.OtherMonthlyIncome,
        ParentNationalities = new List<string>(s.ParentNationalities),
        ApplicableSchemes = s.ApplicableSchemes,
        ApplicableSchemeNames = s.ApplicableSchemeNames,
        RequiredCriteriaTypes = s.RequiredCriteriaTypes,
        ProfileConfirmedFacts = s.ProfileConfirmedFacts,
        UserRequiredFacts = s.UserRequiredFacts,
        RecommendationMatches = s.RecommendationMatches,
        ClarificationField = s.ClarificationField,
        ValidationMessage = s.ValidationMessage,
        PendingParentNationalitySuggestion = s.PendingParentNationalitySuggestion,
        ClarificationAttempts = new Dictionary<string, int>(s.ClarificationAttempts),
        HelpAttempts = new Dictionary<string, int>(s.HelpAttempts),
    };

    private static void Restore(FasInterviewData s, FasInterviewData snapshot)
    {
        s.Status = snapshot.Status;
        s.IsWelfareHomeResident = snapshot.IsWelfareHomeResident;
        s.Email = snapshot.Email;
        s.EmploymentStatusCode = snapshot.EmploymentStatusCode;
        s.MonthlyHouseholdIncome = snapshot.MonthlyHouseholdIncome;
        s.HouseholdMemberCount = snapshot.HouseholdMemberCount;
        s.OtherMonthlyIncome = snapshot.OtherMonthlyIncome;
        s.ParentNationalities = snapshot.ParentNationalities;
        s.ClarificationField = snapshot.ClarificationField;
        s.ValidationMessage = snapshot.ValidationMessage;
        s.PendingParentNationalitySuggestion = snapshot.PendingParentNationalitySuggestion;
        s.ClarificationAttempts = snapshot.ClarificationAttempts;
        s.HelpAttempts = snapshot.HelpAttempts;
    }

    internal static string ProfileFactsIntro(FasInterviewData s)
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
            0 when s.RequiredCriteriaTypes.Count > 0 || s.ApplicableSchemeNames.Count > 0 => "I did not find an open FAS scheme for your school yet.",
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

    internal static string? AcceptedFieldAcknowledgement(string? field, FasInterviewData s) => field switch
    {
        "isWelfareHomeResident" when s.IsWelfareHomeResident.HasValue => s.IsWelfareHomeResident.Value
            ? "Got it - I recorded that you are residing in an approved welfare home."
            : "Got it - I recorded that you are not residing in an approved welfare home.",
        "monthlyHouseholdIncome" when s.MonthlyHouseholdIncome.HasValue => $"Got it - I recorded monthly household income as {s.MonthlyHouseholdIncome.Value.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.",
        "householdMemberCount" when s.HouseholdMemberCount.HasValue => $"Got it - I recorded {s.HouseholdMemberCount.Value} household member{(s.HouseholdMemberCount.Value == 1 ? "" : "s")}.",
        "otherMonthlyIncome" when s.OtherMonthlyIncome.HasValue => $"Got it - I recorded other monthly household income as {s.OtherMonthlyIncome.Value.ToString("C", CultureInfo.GetCultureInfo("en-SG"))}.",
        "employmentStatusCode" when s.EmploymentStatusCode is not null => $"Got it - I recorded employment status as {FasExtractionService.MapEmploymentStatus(s.EmploymentStatusCode)}.",
        "email" when s.Email is not null => $"Got it - I recorded {s.Email} as the application email.",
        "parentNationalities" when s.ParentNationalities.Count > 0 => $"Got it - I recorded parent or guardian nationality as {string.Join(", ", s.ParentNationalities)}.",
        _ => null
    };

    internal static string MapEmploymentStatus(string value) => value switch
    {
        "SELF_EMPLOYED" => "self-employed",
        "UNEMPLOYED" => "unemployed",
        "EMPLOYED" => "employed",
        _ => value
    };

    internal static string HelpForField(string field) => field switch
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

    internal static bool LooksLikeFieldHelpRequest(string message)
    {
        if (Regex.IsMatch(message, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase))
            return false;

        return Regex.IsMatch(message, @"\b(what are|what is|options|option|choose|choices|example|examples|not sure|don't know|do not know|idk|help|does it count|should i count|count as|which one)\b", RegexOptions.IgnoreCase);
    }

    internal static bool LooksLikeUncertaintyAnswer(string message) =>
        Regex.IsMatch(message, @"\b(not sure|don't know|do not know|idk|still not sure|unsure|uncertain)\b", RegexOptions.IgnoreCase);

    internal static bool LooksLikeWelfareHomeCorrection(string message, bool? currentWelfareHome)
    {
        if (Regex.IsMatch(message, @"\b(welfare|approved home|approved welfare|residing in one|live in one|have it|do have)\b", RegexOptions.IgnoreCase))
            return true;

        if (!currentWelfareHome.HasValue)
            return false;

        return Regex.IsMatch(message, @"\b(wait|actually|sorry|correction|meant)\b.{0,20}\b(yes|y|no|n)\b", RegexOptions.IgnoreCase);
    }

    private static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
