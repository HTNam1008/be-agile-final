using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class LocalClaimsTransformation(MoeDbContext dbContext, IClock clock) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(x => x.Type == LocalIdentityClaimNames.UserAccountId))
        {
            return principal;
        }

        string? issuer = principal.FindFirstValue("iss");
        string? subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        string? externalObjectId = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        string? externalTenantId = principal.FindFirstValue("tid")
            ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");
        string? expectedIdentityProviderCode = ResolveExpectedIdentityProviderCode(principal);

        if (string.IsNullOrWhiteSpace(issuer)
            || string.IsNullOrWhiteSpace(expectedIdentityProviderCode))
        {
            return principal;
        }

        AccountLoginState? accountState = await dbContext.Set<UserAccount>()
            .Where(x => x.IdentityProviderCode == expectedIdentityProviderCode
                && ((!string.IsNullOrWhiteSpace(subject)
                        && x.ExternalIssuer == issuer
                        && x.ExternalSubjectId == subject)
                    || (!string.IsNullOrWhiteSpace(externalObjectId)
                        && x.ExternalObjectId == externalObjectId
                        && (x.ExternalTenantId == null || x.ExternalTenantId == externalTenantId))))
            .Select(account => new AccountLoginState(
                account,
                account.PersonId.HasValue
                    ? dbContext.Set<Person>()
                        .Where(person => person.Id == account.PersonId.Value)
                        .Select(person => person.PersonStatusCode)
                        .SingleOrDefault()
                    : null))
            .SingleOrDefaultAsync();
        UserAccount? account = accountState?.Account;

        if (account is null || !account.IsActiveForLogin)
        {
            return principal;
        }

        if (account.PersonId.HasValue
            && accountState!.PersonStatusCode != PersonStatusCodes.Active)
        {
            return principal;
        }

        ClaimsIdentity identity = new("MoeLocalIdentity");
        identity.AddClaim(new Claim(LocalIdentityClaimNames.UserAccountId, account.Id.ToString()));
        identity.AddClaim(new Claim(LocalIdentityClaimNames.IdentityProvider, account.IdentityProviderCode));
        identity.AddClaim(new Claim(LocalIdentityClaimNames.Portal, account.PortalAccessCode));

        if (account.PersonId.HasValue)
        {
            identity.AddClaim(new Claim(LocalIdentityClaimNames.PersonId, account.PersonId.Value.ToString()));
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        string[] roles = await AddAccessClaimsAsync(identity, account.Id, utcNow);

        if (!HasRequiredPortalRole(account, expectedIdentityProviderCode, roles))
        {
            return principal;
        }

        if (account.AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin)
        {
            account.ActivateFirstLogin(utcNow);
            await dbContext.SaveChangesAsync();
        }

        principal.AddIdentity(identity);
        return principal;
    }

    private sealed record AccountLoginState(UserAccount Account, string? PersonStatusCode);

    private async Task<string[]> AddAccessClaimsAsync(ClaimsIdentity identity, long userAccountId, DateTime utcNow)
    {
        List<UserAccessScope> scopes = await dbContext.Set<UserAccessScope>()
            .Where(x => x.UserAccountId == userAccountId)
            .ToListAsync();

        string[] roles = scopes
            .Where(x => x.IsEffective(utcNow))
            .Select(x => x.RoleCode)
            .Distinct()
            .ToArray();

        foreach (UserAccessScope scope in scopes.Where(x => x.IsEffective(utcNow)))
        {
            identity.AddClaim(new Claim(LocalIdentityClaimNames.OrganizationUnitId, scope.OrganizationUnitId.ToString()));
        }

        foreach (string role in roles)
        {
            identity.AddClaim(new Claim(LocalIdentityClaimNames.Role, role));
        }

        string[] permissions = await dbContext.Set<RolePermission>()
            .Where(x => roles.Contains(x.RoleCode)
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow))
            .Select(x => x.PermissionCode)
            .Distinct()
            .ToArrayAsync();

        foreach (string permission in permissions)
        {
            identity.AddClaim(new Claim(LocalIdentityClaimNames.Permission, permission));
        }

        return roles;
    }

    private static bool HasRequiredPortalRole(UserAccount account, string identityProviderCode, IReadOnlyCollection<string> roles)
    {
        return identityProviderCode switch
        {
            IdentityProviderCodes.EntraWorkforce => account.PortalAccessCode == PortalAccessCodes.Admin
                && roles.Any(x => x is RoleCodes.HqAdmin or RoleCodes.SchoolAdmin),
            IdentityProviderCodes.Singpass => account.PortalAccessCode == PortalAccessCodes.EService
                && account.PersonId.HasValue
                && roles.Contains(RoleCodes.Student),
            _ => false
        };
    }

    private static string? ResolveExpectedIdentityProviderCode(ClaimsPrincipal principal)
    {
        string? authenticationScheme = principal.FindFirstValue(LocalIdentityClaimNames.ExternalAuthenticationScheme);

        return authenticationScheme switch
        {
            AuthenticationSchemes.AdminEntra => IdentityProviderCodes.EntraWorkforce,
            AuthenticationSchemes.EServiceSingpass => IdentityProviderCodes.Singpass,
            _ => null
        };
    }
}
