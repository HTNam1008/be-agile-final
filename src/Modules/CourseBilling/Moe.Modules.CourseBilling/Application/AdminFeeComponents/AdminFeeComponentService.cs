using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal sealed class AdminFeeComponentService(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin) : IAdminFeeComponentService
{
    private static readonly string[] SupportedCalculationTypes = ["FIXED", "PERCENTAGE"];

    public async Task<Result<PageResponse<FeeComponentDto>>> ListAsync(FeeComponentQueryRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<PageResponse<FeeComponentDto>>.Failure(admin.Error);

        return Result<PageResponse<FeeComponentDto>>.Success(await feeComponents.ListAsync(request, cancellationToken));
    }

    public async Task<Result<FeeComponentDto>> GetAsync(long feeComponentId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<FeeComponentDto>.Failure(admin.Error);

        FeeComponent? feeComponent = await feeComponents.FindAsync(feeComponentId, cancellationToken);
        return feeComponent is null
            ? Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound)
            : Result<FeeComponentDto>.Success(ToDto(feeComponent));
    }

    public async Task<Result<FeeComponentDto>> CreateAsync(CreateFeeComponentRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<FeeComponentDto>.Failure(admin.Error);

        string componentTypeCode = NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = NormalizeCode(request.CalculationTypeCode);

        Result validation = await ValidateAsync(request.ComponentCode, componentTypeCode, calculationTypeCode, null, cancellationToken);
        if (validation.IsFailure) return Result<FeeComponentDto>.Failure(validation.Error);

        FeeComponent feeComponent = new(
            request.ComponentCode,
            request.ComponentName,
            componentTypeCode,
            calculationTypeCode,
            componentTypeCode == FeeComponentTypeCodes.Tax,
            request.IsActive);

        await feeComponents.AddAsync(feeComponent, cancellationToken);
        return Result<FeeComponentDto>.Success(ToDto(feeComponent));
    }

    public async Task<Result<FeeComponentDto>> UpdateAsync(long feeComponentId, UpdateFeeComponentRequest request, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<FeeComponentDto>.Failure(admin.Error);

        FeeComponent? feeComponent = await feeComponents.FindAsync(feeComponentId, cancellationToken);
        if (feeComponent is null) return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);

        string componentTypeCode = NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = NormalizeCode(request.CalculationTypeCode);

        Result validation = await ValidateAsync(request.ComponentCode, componentTypeCode, calculationTypeCode, feeComponentId, cancellationToken);
        if (validation.IsFailure) return Result<FeeComponentDto>.Failure(validation.Error);

        feeComponent.Update(
            request.ComponentCode,
            request.ComponentName,
            componentTypeCode,
            calculationTypeCode,
            componentTypeCode == FeeComponentTypeCodes.Tax,
            request.IsActive);

        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(ToDto(feeComponent));
    }

    public async Task<Result<FeeComponentDto>> ActivateAsync(long feeComponentId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<FeeComponentDto>.Failure(admin.Error);

        FeeComponent? feeComponent = await feeComponents.FindAsync(feeComponentId, cancellationToken);
        if (feeComponent is null) return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);

        feeComponent.Activate();
        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(ToDto(feeComponent));
    }

    public async Task<Result<FeeComponentDto>> DeactivateAsync(long feeComponentId, CancellationToken cancellationToken)
    {
        Result admin = RequireAdmin();
        if (admin.IsFailure) return Result<FeeComponentDto>.Failure(admin.Error);

        FeeComponent? feeComponent = await feeComponents.FindAsync(feeComponentId, cancellationToken);
        if (feeComponent is null) return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);

        feeComponent.Deactivate();
        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(ToDto(feeComponent));
    }

    private async Task<Result> ValidateAsync(
        string componentCode,
        string componentTypeCode,
        string calculationTypeCode,
        long? excludeFeeComponentId,
        CancellationToken cancellationToken)
    {
        if (!FeeComponentTypeCodes.All.Contains(componentTypeCode, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(CourseErrors.InvalidFeeComponentType);
        }

        if (!SupportedCalculationTypes.Contains(calculationTypeCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(CourseErrors.InvalidCalculationType);
        }

        if (await feeComponents.ComponentCodeExistsAsync(componentCode, excludeFeeComponentId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateFeeComponentCode);
        }

        return Result.Success();
    }

    private Result RequireAdmin()
        => currentAdmin.IsAdmin ? Result.Success() : Result.Failure(CourseErrors.AdminRequired);

    private static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant();

    private static FeeComponentDto ToDto(FeeComponent feeComponent)
        => new(
            feeComponent.Id,
            feeComponent.ComponentCode,
            feeComponent.ComponentName,
            feeComponent.ComponentTypeCode,
            feeComponent.CalculationTypeCode,
            feeComponent.IsTaxComponent,
            feeComponent.IsActive);
}
