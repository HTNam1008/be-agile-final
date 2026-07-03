namespace Moe.Modules.FasPayment.Application.StudentApplications;

public sealed record EligibilityRequest(decimal MonthlyHouseholdIncome, int HouseholdMemberCount, decimal OtherMonthlyIncome = 0,
    IReadOnlyCollection<string>? ParentNationalities = null);
public sealed record EligibilityResponse(
    object CurrentSchool,
    int Age,
    string Nationality,
    decimal MonthlyHouseholdIncome,
    int HouseholdMemberCount,
    IReadOnlyCollection<string> ParentNationalities,
    string AccountType,
    decimal PerCapitaIncome,
    IReadOnlyCollection<EligibilitySchemeMatch> MatchedSchemes);
public sealed record EligibilitySchemeMatch(
    long SchemeId,
    string SchemeName,
    string? Description,
    long TierId,
    string TierLabel,
    string SubsidyType,
    decimal SubsidyValue,
    DateOnly ApplicationEndDate,
    int RecommendationRank,
    string RecommendationReason);
public sealed record EligibilitySchemeOption(long Id, string Name);
public sealed record EligibilityCriteriaPlan(
    IReadOnlyCollection<EligibilitySchemeOption> ApplicableSchemes,
    IReadOnlyCollection<string> ApplicableSchemeNames,
    IReadOnlyCollection<string> RequiredCriteriaTypes,
    IReadOnlyCollection<string> ProfileConfirmedFacts,
    IReadOnlyCollection<string> UserRequiredFacts);
public sealed record CreateDraftRequest(IReadOnlyCollection<long> SchemeIds);
public sealed record ReplaceSchemesRequest(IReadOnlyCollection<long> SchemeIds);
public sealed record UpdateParticularsRequest(string Email, IReadOnlyCollection<string> ParentNationalities);
public sealed record UpdateIncomeRequest(bool IsWelfareHomeResident, string? EmploymentStatusCode,
    decimal? MonthlyHouseholdIncome, int? HouseholdMemberCount, decimal OtherMonthlyIncome = 0);
public sealed record SaveDeclarationsRequest(bool TrueAndAccurate, bool AcceptTerms,
    string TrueAndAccurateText, string AcceptTermsText);
public sealed record AdminApproveSchemeRequest(long TierId, string? Remarks);
public sealed record AdminRejectSchemeRequest(string Notes);
public sealed record DocumentScanResultRequest(bool Passed);
