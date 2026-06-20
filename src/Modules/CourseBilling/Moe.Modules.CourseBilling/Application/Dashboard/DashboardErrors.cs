using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard;

internal static class DashboardErrors
{
    public static readonly Error AuthenticatedStudentRequired = new(
        "DASHBOARD.AUTHENTICATED_STUDENT_REQUIRED",
        "An authenticated e-service student is required.");

    public static readonly Error AuthenticatedAdminRequired = new(
        "DASHBOARD.AUTHENTICATED_ADMIN_REQUIRED",
        "An authenticated admin account is required.");

    public static readonly Error AdminProfileNotFound = new(
        "DASHBOARD.ADMIN_PROFILE_NOT_FOUND",
        "The admin profile was not found.");

    public static readonly Error AdminOrganizationScopeRequired = new(
        "DASHBOARD.ADMIN_ORGANIZATION_SCOPE_REQUIRED",
        "A current admin organization scope is required.");

    public static readonly Error InvalidDashboardYear = new(
        "DASHBOARD.INVALID_YEAR",
        "Dashboard year must be between 2000 and 2100.");

    public static readonly Error StudentNotFound = new(
        "DASHBOARD.STUDENT_NOT_FOUND",
        "The student profile was not found.");

    public static readonly Error EducationAccountNotFound = new(
        "DASHBOARD.EDUCATION_ACCOUNT_NOT_FOUND",
        "The education account was not found.");
}
