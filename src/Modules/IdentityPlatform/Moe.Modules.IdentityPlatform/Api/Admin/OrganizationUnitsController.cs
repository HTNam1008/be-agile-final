using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Application.Organizations;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/organization-units")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class OrganizationUnitsController(
    MoeDbContext dbContext,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        IQueryable<OrganizationUnit> query = dbContext.Set<OrganizationUnit>().AsNoTracking();

        if (!currentUser.HasPermission("ORG_VIEW_ALL"))
        {
            long[] scopedOrganizationIds = currentUser.OrganizationUnitIds.ToArray();
            query = query.Where(x => scopedOrganizationIds.Contains(x.Id));
        }

        OrganizationUnitSummary[] organizations = await query
            .Where(x => x.StatusCode == "ACTIVE")
            .OrderBy(x => x.UnitTypeCode)
            .ThenBy(x => x.UnitName)
            .Select(x => new OrganizationUnitSummary(
                x.Id,
                x.ParentOrganizationUnitId,
                x.UnitCode,
                x.UnitName,
                x.UnitTypeCode,
                x.StatusCode))
            .ToArrayAsync(cancellationToken);

        return ApiResponseFactory.Ok(organizations, HttpContext.TraceIdentifier);
    }
}
