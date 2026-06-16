using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moe.Application.Abstractions.Security;

namespace Moe.Infrastructure.Shared.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public long? UserAccountId => Parse(ClaimNames.UserAccountId);
    public long? PersonId => Parse(ClaimNames.PersonId);
    public long? OrganizationUnitId => Parse(ClaimNames.OrganizationUnitId);
    public IReadOnlyCollection<long> OrganizationUnitIds => User.FindAll(ClaimNames.OrganizationUnitId)
        .Select(x => long.TryParse(x.Value, out var value) ? value : (long?)null)
        .Where(x => x.HasValue)
        .Select(x => x!.Value)
        .Distinct()
        .ToArray();
    public IReadOnlyCollection<string> Roles => User.FindAll(ClaimNames.Role).Select(x => x.Value).Distinct().ToArray();
    public IReadOnlyCollection<string> Permissions => User.FindAll(ClaimNames.Permission).Select(x => x.Value).Distinct().ToArray();
    public string Portal => User.FindFirstValue(ClaimNames.Portal) ?? string.Empty;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public bool HasPermission(string permission) => User.FindAll(ClaimNames.Permission).Any(x => x.Value == permission);
    private long? Parse(string claim) => long.TryParse(User.FindFirstValue(claim), out var value) ? value : null;
}
