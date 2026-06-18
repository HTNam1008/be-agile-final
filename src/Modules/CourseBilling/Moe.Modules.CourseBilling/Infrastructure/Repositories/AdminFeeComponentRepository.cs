using Microsoft.EntityFrameworkCore;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class AdminFeeComponentRepository(MoeDbContext dbContext) : IAdminFeeComponentRepository
{
    public async Task<PageResponse<FeeComponentDto>> ListAsync(FeeComponentQueryRequest request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        IQueryable<FeeComponent> query = dbContext.Set<FeeComponent>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            string keyword = request.Keyword.Trim();
            query = query.Where(x => x.ComponentCode.Contains(keyword) || x.ComponentName.Contains(keyword));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == request.IsActive.Value);
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<FeeComponentDto> items = await query
            .OrderBy(x => x.ComponentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new FeeComponentDto(
                x.Id,
                x.ComponentCode,
                x.ComponentName,
                x.ComponentTypeCode,
                x.CalculationTypeCode,
                x.IsTaxComponent,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PageResponse<FeeComponentDto>(items, page, pageSize, total);
    }

    public Task<FeeComponent?> FindAsync(long feeComponentId, CancellationToken cancellationToken)
        => dbContext.Set<FeeComponent>().SingleOrDefaultAsync(x => x.Id == feeComponentId, cancellationToken);

    public Task<bool> ComponentCodeExistsAsync(string componentCode, long? excludeFeeComponentId, CancellationToken cancellationToken)
    {
        string normalizedCode = componentCode.Trim();

        return dbContext.Set<FeeComponent>().AnyAsync(x =>
            x.ComponentCode == normalizedCode
            && (excludeFeeComponentId == null || x.Id != excludeFeeComponentId.Value),
            cancellationToken);
    }

    public async Task AddAsync(FeeComponent feeComponent, CancellationToken cancellationToken)
    {
        await dbContext.Set<FeeComponent>().AddAsync(feeComponent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
