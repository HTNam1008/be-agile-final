using System.Globalization;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.CourseBilling.Application.Dashboard;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.AdminDashboard;
using Moe.Modules.IdentityPlatform.IGateway.AdminDashboard;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.GetAdminDashboard;

internal sealed class GetAdminDashboardHandler(
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IAdminDashboardIdentityDirectory identities,
    IAdminDashboardTopUpDirectory topUps,
    IAdminDashboardCourseRepository courses)
    : IQueryHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    private static readonly CultureInfo SingaporeCulture = CultureInfo.GetCultureInfo("en-SG");

    public async Task<Result<AdminDashboardResponse>> Handle(
        GetAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated
            || currentUser.UserAccountId is null
            || currentUser.Portal != PortalCodes.Admin)
        {
            return Result<AdminDashboardResponse>.Failure(DashboardErrors.AuthenticatedAdminRequired);
        }

        AdminOrganizationScope organizationScope = adminAccess.ResolveOrganizationFilter(query.OrganizationId);
        if (!organizationScope.HasAccess)
        {
            return Result<AdminDashboardResponse>.Failure(DashboardErrors.AdminOrganizationScopeRequired);
        }
        long? organizationId = organizationScope.OrganizationId;

        int year = query.Year.GetValueOrDefault(clock.UtcNow.Year);
        if (year is < 2000 or > 2100)
        {
            return Result<AdminDashboardResponse>.Failure(DashboardErrors.InvalidDashboardYear);
        }

        AdminDashboardIdentitySummary? identity = await identities.GetSummaryAsync(
            currentUser.UserAccountId.Value,
            organizationId,
            cancellationToken);

        if (identity is null)
        {
            return Result<AdminDashboardResponse>.Failure(DashboardErrors.AdminProfileNotFound);
        }

        long totalCourses = await courses.CountCoursesAsync(organizationId, cancellationToken);
        AdminDashboardTopUpSummary topUpSummary = await topUps.GetSummaryAsync(
            organizationId,
            year,
            clock.UtcNow.UtcDateTime,
            cancellationToken);

        return Result<AdminDashboardResponse>.Success(new AdminDashboardResponse(
            new AdminDashboardProfileResponse(
                identity.AdminUserAccountId,
                identity.DisplayName,
                identity.OrganizationId,
                identity.OrganizationName),
            new AdminDashboardCardsResponse(
                new AdminDashboardCountCardResponse(
                    "Total Schools",
                    identity.TotalSchools,
                    ToNumberDisplay(identity.TotalSchools)),
                new AdminDashboardCountCardResponse(
                    "Total Student Active",
                    identity.TotalActiveStudents,
                    ToNumberDisplay(identity.TotalActiveStudents)),
                new AdminDashboardCountCardResponse(
                    "Active Course",
                    totalCourses,
                    ToNumberDisplay(totalCourses)),
                new AdminDashboardAmountCardResponse(
                    "Total Top up monthly",
                    topUpSummary.MonthlyTotalAmount,
                    topUpSummary.CurrencyCode,
                    ToAmountDisplay(topUpSummary.CurrencyCode, topUpSummary.MonthlyTotalAmount))),
            new AdminDashboardTopUpYearlyResponse(
                year,
                topUpSummary.CurrencyCode,
                topUpSummary.YearlySeries.Select(point => new AdminDashboardTopUpPointResponse(
                    point.Month,
                    ToMonthLabel(point.Month),
                    point.Amount,
                    ToAmountDisplay(topUpSummary.CurrencyCode, point.Amount))).ToArray())));
    }

    private static string ToNumberDisplay(long value)
        => value.ToString("N0", SingaporeCulture);

    private static string ToAmountDisplay(string currencyCode, decimal amount)
        => string.Create(SingaporeCulture, $"{amount:N0} {currencyCode}");

    private static string ToMonthLabel(int month)
        => SingaporeCulture.DateTimeFormat.GetAbbreviatedMonthName(month);
}
