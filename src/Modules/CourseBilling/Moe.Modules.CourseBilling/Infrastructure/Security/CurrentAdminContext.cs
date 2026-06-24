using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Infrastructure.Security;

internal sealed class CurrentAdminContext(ICurrentUser currentUser) : ICurrentAdminContext
{
    public string RoleCode => currentUser.Roles.FirstOrDefault() ?? string.Empty;
    public bool IsAdmin => currentUser.IsAuthenticated
        && currentUser.Roles.Any(role => role is CourseBillingRoles.HqAdmin or CourseBillingRoles.SchoolAdmin);
}
