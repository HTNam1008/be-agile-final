using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Application.Organizations;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class OrganizationUnitRepository(MoeDbContext dbContext) : IOrganizationUnitRepository
{
    public async Task<IReadOnlyCollection<OrganizationUnitSummary>> ListActiveAsync(
        IReadOnlyCollection<long>? organizationIds,
        CancellationToken cancellationToken)
    {
        IQueryable<OrganizationUnit> query = dbContext.Set<OrganizationUnit>()
            .AsNoTracking()
            .Where(x => x.StatusCode == IamStatusCodes.Active);

        if (organizationIds is { Count: > 0 })
        {
            query = query.Where(x => organizationIds.Contains(x.Id));
        }

        return await query
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
    }

    public Task<OrganizationUnitSummary?> FindActiveByIdAsync(
        long organizationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<OrganizationUnit>()
            .AsNoTracking()
            .Where(x => x.Id == organizationId && x.StatusCode == IamStatusCodes.Active)
            .Select(x => new OrganizationUnitSummary(
                x.Id,
                x.ParentOrganizationUnitId,
                x.UnitCode,
                x.UnitName,
                x.UnitTypeCode,
                x.StatusCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<OrganizationUnitSummary?> FindActiveSchoolByNameAsync(
        string schoolName,
        CancellationToken cancellationToken)
    {
        string normalizedSchoolName = schoolName.Trim().ToUpperInvariant();

        return ActiveSchools()
            .Where(x => x.UnitName.ToUpper() == normalizedSchoolName)
            .Select(x => new OrganizationUnitSummary(
                x.Id,
                x.ParentOrganizationUnitId,
                x.UnitCode,
                x.UnitName,
                x.UnitTypeCode,
                x.StatusCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<OrganizationUnitSummary?> FindActiveSchoolByIdAsync(
        long organizationId,
        CancellationToken cancellationToken)
    {
        return ActiveSchools()
            .Where(x => x.Id == organizationId)
            .Select(x => new OrganizationUnitSummary(
                x.Id,
                x.ParentOrganizationUnitId,
                x.UnitCode,
                x.UnitName,
                x.UnitTypeCode,
                x.StatusCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private IQueryable<OrganizationUnit> ActiveSchools()
    {
        return dbContext.Set<OrganizationUnit>()
            .AsNoTracking()
            .Where(x => x.StatusCode == IamStatusCodes.Active
                && x.UnitTypeCode == "SCHOOL");
    }
}
