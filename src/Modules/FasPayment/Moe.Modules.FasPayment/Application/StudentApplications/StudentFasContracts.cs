namespace Moe.Modules.FasPayment.Application.StudentApplications;

public sealed record EligibilityRequest(decimal MonthlyHouseholdIncome, int HouseholdMemberCount, decimal OtherMonthlyIncome = 0,
    IReadOnlyCollection<string>? ParentNationalities = null);
public sealed record CreateDraftRequest(IReadOnlyCollection<long> SchemeIds);
public sealed record ReplaceSchemesRequest(IReadOnlyCollection<long> SchemeIds);
public sealed record UpdateParticularsRequest(string Email, IReadOnlyCollection<string> ParentNationalities);
public sealed record UpdateIncomeRequest(bool IsWelfareHomeResident, string? EmploymentStatusCode,
    decimal? MonthlyHouseholdIncome, int? HouseholdMemberCount, decimal OtherMonthlyIncome = 0);
public sealed record SaveDeclarationsRequest(bool TrueAndAccurate, bool AcceptTerms,
    string TrueAndAccurateText, string AcceptTermsText);
public sealed record AdminApproveSchemeRequest(decimal Amount, object? Components, DateOnly ValidFrom, DateOnly ValidTo, string? Remarks);
public sealed record AdminRejectSchemeRequest(string Notes);
public sealed record DocumentScanResultRequest(bool Passed);
