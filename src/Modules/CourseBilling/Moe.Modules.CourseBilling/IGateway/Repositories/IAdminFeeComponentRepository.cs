using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface IAdminFeeComponentRepository
{
    Task<PageResponse<FeeComponentDto>> ListAsync(FeeComponentQueryRequest request, CancellationToken cancellationToken);
    Task<FeeComponent?> FindAsync(long feeComponentId, CancellationToken cancellationToken);
    Task<bool> ComponentCodeExistsAsync(string componentCode, long? excludeFeeComponentId, CancellationToken cancellationToken);
    Task<bool> IsInUseAsync(long feeComponentId, CancellationToken cancellationToken);
    Task AddAsync(FeeComponent feeComponent, CancellationToken cancellationToken);
    Task SaveAsync(FeeComponent feeComponent, CancellationToken cancellationToken);
    Task DeleteAsync(FeeComponent feeComponent, CancellationToken cancellationToken);
}
