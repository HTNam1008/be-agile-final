using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.FasPayment.Application.StudentApplications;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class FasEligibilityService(StudentFasApplicationService fas, ILogger<FasEligibilityService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = AiJsonOptions.Default;

    public async Task<(string Text, FasInterviewData State, FasRecommendationMatch[] Schemes, object? Recommendation)> ComputeEligibility(FasInterviewData state, CancellationToken ct)
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
        catch (Exception ex)
        {
            logger.LogError(ex, "FAS eligibility check failed for status {Status}", state.Status);

            bool isTransient = ex is HttpRequestException
                or TaskCanceledException
                or OperationCanceledException
                or TimeoutException;

            if (isTransient)
            {
                state.Status = "MANUAL_FALLBACK";
                return ("I was unable to check eligibility right now due to a connection issue. Please try again in a moment, or use 'Open FAS application' to complete the form manually.",
                    state, [], null);
            }

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

    internal static FasRecommendationCard BuildFasRecommendation(JsonElement root, AiInterviewState interview)
    {
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

    internal static decimal BenefitRank(FasRecommendationMatch match)
    {
        string subsidyType = match.SubsidyType.ToUpperInvariant();
        return subsidyType switch
        {
            "PERCENTAGE" => match.IsComparable ? match.SubsidyValue * 1000m : 0m,
            "FIXED" => match.SubsidyValue,
            _ => 0m
        };
    }

    internal static bool CanPrepareOpenSchemeForReview(FasInterviewData state) =>
        state.ApplicableSchemes.Count > 0 && state.RequiredCriteriaTypes.Count == 0;

    internal static FasRecommendationMatch[] ReviewRequiredSchemeMatches(FasInterviewData state) =>
        state.ApplicableSchemes
            .Select((scheme, index) => new FasRecommendationMatch(scheme.Id, scheme.Name, 0, "Review required", "Scheme selection", 0m,
                index + 1, "Open scheme for your school. Criteria are not fully configured for automatic ranking, so staff/form review is required.", "REVIEW_REQUIRED", false))
            .ToArray();

    internal static FasRecommendationCard BuildReviewRequiredRecommendation(AiInterviewState interview, IReadOnlyCollection<FasRecommendationMatch> matches)
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

    internal static FasRecommendationMatch[] ExtractRecommendationMatches(JsonElement root)
    {
        return root.TryGetProperty("matchedSchemes", out JsonElement schemes) && schemes.ValueKind == JsonValueKind.Array
            ? schemes.EnumerateArray().Select(ToRecommendationMatch).Where(x => x is not null).Cast<FasRecommendationMatch>().ToArray()
            : [];
    }

    internal static FasRecommendationMatch[] WelfareHomeRecommendationMatches(FasInterviewData state)
        => state.ApplicableSchemes
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Select((x, index) => new FasRecommendationMatch(x.Id, x.Name, 0, "Welfare-home route", "ASSISTANCE", 0m,
                index + 1, "Open scheme for your school. Welfare-home applicants skip income-based ranking and must review the scheme selection in the form.", "REVIEW_REQUIRED", false))
            .ToArray();

    internal static string? TryGetString(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    internal static long? TryGetInt64(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result) ? result : null;
    internal static int? TryGetInt32(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int result) ? result : null;
    internal static decimal? TryGetDecimal(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out decimal result) ? result : null;
    internal static bool? TryGetBoolean(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

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

    private static AiInterviewState ToInterviewState(FasInterviewData s, string? next, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
        => FasConfirmationService.ToInterviewState(s, next, recommendedSchemes);
}
