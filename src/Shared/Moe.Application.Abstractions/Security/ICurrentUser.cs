namespace Moe.Application.Abstractions.Security;

public interface ICurrentUser
{
    long? UserAccountId { get; }
    long? PersonId { get; }
    long? OrganizationUnitId { get; }
    IReadOnlyCollection<long> OrganizationUnitIds { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    string Portal { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
}
