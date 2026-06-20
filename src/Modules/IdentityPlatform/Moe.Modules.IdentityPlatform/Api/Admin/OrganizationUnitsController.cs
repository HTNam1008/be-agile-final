using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/organization-units")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class OrganizationUnitsController(
    IOrganizationUnitRepository organizations,
    IAdminAccessControl adminAccess) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long>? organizationIds = adminAccess.IsHqAdmin
            ? null
            : adminAccess.ScopedOrganizationIds;

        var result = await organizations.ListActiveAsync(organizationIds, cancellationToken);

        return ApiResponseFactory.Ok(result, HttpContext.TraceIdentifier);
    }
}
