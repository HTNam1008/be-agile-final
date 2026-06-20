namespace Moe.Modules.CourseBilling.Application.Dashboard.GetAdminDashboard;

public sealed record AdminDashboardResponse(
    AdminDashboardProfileResponse Admin,
    AdminDashboardCardsResponse Cards,
    AdminDashboardTopUpYearlyResponse TopUpYearly);

public sealed record AdminDashboardProfileResponse(
    long UserAccountId,
    string DisplayName,
    long? OrganizationId,
    string? OrganizationName);

public sealed record AdminDashboardCardsResponse(
    AdminDashboardCountCardResponse TotalSchools,
    AdminDashboardCountCardResponse TotalStudents,
    AdminDashboardCountCardResponse TotalCourses,
    AdminDashboardAmountCardResponse MonthlyTopUpAmount);

public sealed record AdminDashboardCountCardResponse(
    string Label,
    long Value,
    string Display);

public sealed record AdminDashboardAmountCardResponse(
    string Label,
    decimal Value,
    string CurrencyCode,
    string Display);

public sealed record AdminDashboardTopUpYearlyResponse(
    int Year,
    string CurrencyCode,
    IReadOnlyCollection<AdminDashboardTopUpPointResponse> Points);

public sealed record AdminDashboardTopUpPointResponse(
    int Month,
    string Label,
    decimal Amount,
    string Display);
