using System.Globalization;
using System.Text.Json;
using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

internal static class FasConfirmationService
{
    private static readonly JsonSerializerOptions JsonOptions = AiJsonOptions.Default;

    internal static string ConfirmationPrompt(FasInterviewData s)
    {
        List<string> facts = [];
        string welfareDisplay = s.IsWelfareHomeResident.HasValue
            ? (s.IsWelfareHomeResident.Value ? "Yes" : "No")
            : "Not answered";
        facts.Add($"Welfare home: {welfareDisplay}");
        string empDisplay = s.EmploymentStatusCode switch
        {
            "EMPLOYED" => "Employed",
            "SELF_EMPLOYED" => "Self-employed",
            "UNEMPLOYED" => "Unemployed",
            _ => "Not provided"
        };
        facts.Add($"Employment status: {empDisplay}");
        if (s.IsWelfareHomeResident == false)
        {
            facts.Add($"Monthly household income: {s.MonthlyHouseholdIncome?.ToString("C", CultureInfo.GetCultureInfo("en-SG")) ?? "Not provided"}");
            facts.Add($"Household members: {s.HouseholdMemberCount?.ToString(CultureInfo.InvariantCulture) ?? "Not provided"}");
            facts.Add($"Other monthly income: {s.OtherMonthlyIncome?.ToString("C", CultureInfo.GetCultureInfo("en-SG")) ?? "Not provided"}");
        }
        facts.Add($"Parent or guardian nationality: {(s.ParentNationalities.Count == 0 ? "Not provided" : string.Join(", ", s.ParentNationalities))}");
        if (s.Email is not null)
            facts.Add($"Application email: {s.Email}");

        return $"Before I calculate FAS eligibility, please confirm these details are correct:\n\n{string.Join("\n", facts.Select(x => $"- {x}"))}\n\nReply yes to calculate eligibility. To correct any detail above, just say what you want to change (e.g. \"actually 2500\" or \"change to Foreigner\").";
    }


    internal static AiInterviewState ToInterviewState(FasInterviewData s, string? next, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        List<AiInterviewField> fields =
        [
            new("isWelfareHomeResident", s.IsWelfareHomeResident, s.IsWelfareHomeResident.HasValue ? "AI_CONFIRMED" : "UNMAPPED", s.IsWelfareHomeResident.HasValue),
            new("email", s.Email ?? FasEligibilityService.TryGetString(s.Profile, "email"), s.Email is not null ? "AI_CONFIRMED" : FasEligibilityService.TryGetString(s.Profile, "email") is not null ? "PROFILE" : "UNMAPPED", s.Email is not null || FasEligibilityService.TryGetString(s.Profile, "email") is not null),
            new("employmentStatusCode", s.EmploymentStatusCode ?? FasEligibilityService.TryGetString(s.Profile, "employmentStatusCode"), s.EmploymentStatusCode is not null ? "AI_CONFIRMED" : FasEligibilityService.TryGetString(s.Profile, "employmentStatusCode") is not null ? "PROFILE" : "UNMAPPED", s.EmploymentStatusCode is not null || FasEligibilityService.TryGetString(s.Profile, "employmentStatusCode") is not null),
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

    internal static FasFormPatch BuildFasFormPatch(FasInterviewData s, IReadOnlyCollection<FasRecommendationMatch>? recommendedSchemes = null)
    {
        var particulars = new FasPatchParticulars(
            Email: s.Email ?? FasEligibilityService.TryGetString(s.Profile, "email"),
            ParentNationalities: s.ParentNationalities.Count > 0 ? s.ParentNationalities.ToArray() : null);
        var income = new FasPatchIncome(
            s.IsWelfareHomeResident,
            s.EmploymentStatusCode ?? FasEligibilityService.TryGetString(s.Profile, "employmentStatusCode") ?? "EMPLOYED",
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
            AddMeta("email", s.Email ?? FasEligibilityService.TryGetString(s.Profile, "email"), s.Email is null ? "PROFILE" : "AI_CONFIRMED", s.Email is null ? "From your profile." : "Confirmed in chat.");
            AddMeta("employmentStatusCode", s.EmploymentStatusCode ?? FasEligibilityService.TryGetString(s.Profile, "employmentStatusCode"), s.EmploymentStatusCode is null ? "PROFILE" : "AI_CONFIRMED", s.EmploymentStatusCode is null ? "From your profile." : "Confirmed in chat.");
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
        if (fieldName is "monthlyHouseholdIncome" or "householdMemberCount" or "otherMonthlyIncome") return s.RequiredCriteriaTypes.Count == 0 && s.ApplicableSchemeNames.Count == 0 || s.RequiredCriteriaTypes.Any(c => c is "GDP" or "GHI" or "PCI") || s.ApplicableSchemes.Count > 0;
        if (fieldName == "parentNationalities") return true;
        if (fieldName is "email" or "employmentStatusCode") return false;
        return true;
    }
}
