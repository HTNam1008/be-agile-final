using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Dashboard.RoleDashboards;

internal static class RoleDashboardErrors
{
    public static readonly Error HqAdminRequired = new(
        "DASHBOARD.HQ_ADMIN_REQUIRED",
        "An HQ administrator is required.");

    public static readonly Error SchoolAdminRequired = new(
        "DASHBOARD.SCHOOL_ADMIN_REQUIRED",
        "A school administrator is required.");

    public static readonly Error SchoolScopeRequired = new(
        "DASHBOARD.SCHOOL_SCOPE_REQUIRED",
        "A single school organization scope is required.");

    public static readonly Error InvalidYear = new(
        "DASHBOARD.INVALID_YEAR",
        "Dashboard year must be between 2000 and 2100.");
}
