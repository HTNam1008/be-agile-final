using Moe.Application.Abstractions.Security;

namespace Moe.Modules.CourseBilling.Infrastructure.Security;

internal sealed class CurrentAdminContext(ICurrentUser currentUser) : ICurrentAdminContext
{
    public string RoleCode => currentUser.Roles.FirstOrDefault() ?? string.Empty;
    // TODO: Temporary development bypass until the admin authentication flow is ready.
    public bool IsAdmin => true;
}
