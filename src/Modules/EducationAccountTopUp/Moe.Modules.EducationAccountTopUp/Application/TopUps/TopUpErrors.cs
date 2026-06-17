using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps;

internal static class TopUpErrors
{
    public static readonly Error AdminOrganizationScopeRequired = new(
        "TOPUP.ADMIN_ORGANIZATION_SCOPE_REQUIRED",
        "An admin organization scope is required.");

    public static readonly Error OrganizationOutsideScope = new(
        "TOPUP.ORGANIZATION_OUTSIDE_SCOPE",
        "The requested organization is outside the admin's scope.");
}
