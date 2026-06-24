namespace Moe.Modules.CourseBilling.Infrastructure.Security;

public interface ICurrentAdminContext
{
    string RoleCode { get; }
    bool IsAdmin { get; }
    bool IsHqAdmin { get; }
}
