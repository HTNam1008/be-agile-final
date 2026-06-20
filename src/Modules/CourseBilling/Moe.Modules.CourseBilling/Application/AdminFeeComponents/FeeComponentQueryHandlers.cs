using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal sealed class ListFeeComponentsQueryHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : IQueryHandler<ListFeeComponentsQuery, PageResponse<FeeComponentDto>>
{
    public async Task<Result<PageResponse<FeeComponentDto>>> Handle(
        ListFeeComponentsQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<PageResponse<FeeComponentDto>>.Failure(CourseErrors.AdminRequired);
        }

        return Result<PageResponse<FeeComponentDto>>.Success(
            await feeComponents.ListAsync(query.Request, cancellationToken));
    }
}

internal sealed class GetFeeComponentQueryHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : IQueryHandler<GetFeeComponentQuery, FeeComponentDto>
{
    public async Task<Result<FeeComponentDto>> Handle(
        GetFeeComponentQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.AdminRequired);
        }

        FeeComponent? feeComponent = await feeComponents.FindAsync(query.FeeComponentId, cancellationToken);
        return feeComponent is null
            ? Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound)
            : Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }
}
