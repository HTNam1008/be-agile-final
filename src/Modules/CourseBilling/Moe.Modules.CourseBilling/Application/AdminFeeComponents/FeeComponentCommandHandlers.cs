using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal sealed class CreateFeeComponentCommandHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : ICommandHandler<CreateFeeComponentCommand, FeeComponentDto>
{
    public async Task<Result<FeeComponentDto>> Handle(
        CreateFeeComponentCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.AdminRequired);
        }

        CreateFeeComponentRequest request = command.Request;
        string componentTypeCode = NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = NormalizeCode(request.CalculationTypeCode);

        Result validation = await ValidateAsync(
            feeComponents,
            request.ComponentCode,
            componentTypeCode,
            calculationTypeCode,
            null,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<FeeComponentDto>.Failure(validation.Error);
        }

        FeeComponent feeComponent = new(
            request.ComponentCode,
            request.ComponentName,
            componentTypeCode,
            calculationTypeCode,
            componentTypeCode == FeeComponentTypeCodes.Tax,
            request.IsActive);

        await feeComponents.AddAsync(feeComponent, cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }

    private static async Task<Result> ValidateAsync(
        IAdminFeeComponentRepository feeComponents,
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

        if (!FeeComponentCalculationTypes.All.Contains(calculationTypeCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(CourseErrors.InvalidCalculationType);
        }

        if (await feeComponents.ComponentCodeExistsAsync(componentCode, excludeFeeComponentId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateFeeComponentCode);
        }

        return Result.Success();
    }

    private static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant();
}

internal sealed class UpdateFeeComponentCommandHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : ICommandHandler<UpdateFeeComponentCommand, FeeComponentDto>
{
    public async Task<Result<FeeComponentDto>> Handle(
        UpdateFeeComponentCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.AdminRequired);
        }

        FeeComponent? feeComponent = await feeComponents.FindAsync(command.FeeComponentId, cancellationToken);
        if (feeComponent is null)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        UpdateFeeComponentRequest request = command.Request;
        string componentTypeCode = NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = NormalizeCode(request.CalculationTypeCode);

        Result validation = await ValidateAsync(
            feeComponents,
            request.ComponentCode,
            componentTypeCode,
            calculationTypeCode,
            command.FeeComponentId,
            cancellationToken);
        if (validation.IsFailure)
        {
            return Result<FeeComponentDto>.Failure(validation.Error);
        }

        feeComponent.Update(
            request.ComponentCode,
            request.ComponentName,
            componentTypeCode,
            calculationTypeCode,
            componentTypeCode == FeeComponentTypeCodes.Tax,
            request.IsActive);

        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }

    private static async Task<Result> ValidateAsync(
        IAdminFeeComponentRepository feeComponents,
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

        if (!FeeComponentCalculationTypes.All.Contains(calculationTypeCode.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(CourseErrors.InvalidCalculationType);
        }

        if (await feeComponents.ComponentCodeExistsAsync(componentCode, excludeFeeComponentId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateFeeComponentCode);
        }

        return Result.Success();
    }

    private static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant();
}

internal sealed class ActivateFeeComponentCommandHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : ICommandHandler<ActivateFeeComponentCommand, FeeComponentDto>
{
    public async Task<Result<FeeComponentDto>> Handle(
        ActivateFeeComponentCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.AdminRequired);
        }

        FeeComponent? feeComponent = await feeComponents.FindAsync(command.FeeComponentId, cancellationToken);
        if (feeComponent is null)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        feeComponent.Activate();
        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }
}

internal sealed class DeactivateFeeComponentCommandHandler(
    IAdminFeeComponentRepository feeComponents,
    ICurrentAdminContext currentAdmin)
    : ICommandHandler<DeactivateFeeComponentCommand, FeeComponentDto>
{
    public async Task<Result<FeeComponentDto>> Handle(
        DeactivateFeeComponentCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentAdmin.IsAdmin)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.AdminRequired);
        }

        FeeComponent? feeComponent = await feeComponents.FindAsync(command.FeeComponentId, cancellationToken);
        if (feeComponent is null)
        {
            return Result<FeeComponentDto>.Failure(CourseErrors.FeeComponentNotFound);
        }

        feeComponent.Deactivate();
        await feeComponents.SaveChangesAsync(cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }
}

internal static class FeeComponentCalculationTypes
{
    public static readonly string[] All = ["FIXED", "PERCENTAGE"];
}
