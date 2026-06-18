using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

public sealed record FeeComponentQueryRequest(
    string? Keyword,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateFeeComponentRequest(
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    bool IsActive = true);

public sealed record UpdateFeeComponentRequest(
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    bool IsActive);

public sealed record FeeComponentDto(
    long FeeComponentId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    bool IsTaxComponent,
    bool IsActive);

public interface IAdminFeeComponentService
{
    Task<Result<PageResponse<FeeComponentDto>>> ListAsync(FeeComponentQueryRequest request, CancellationToken cancellationToken);
    Task<Result<FeeComponentDto>> GetAsync(long feeComponentId, CancellationToken cancellationToken);
    Task<Result<FeeComponentDto>> CreateAsync(CreateFeeComponentRequest request, CancellationToken cancellationToken);
    Task<Result<FeeComponentDto>> UpdateAsync(long feeComponentId, UpdateFeeComponentRequest request, CancellationToken cancellationToken);
    Task<Result<FeeComponentDto>> ActivateAsync(long feeComponentId, CancellationToken cancellationToken);
    Task<Result<FeeComponentDto>> DeactivateAsync(long feeComponentId, CancellationToken cancellationToken);
}
