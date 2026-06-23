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
        string componentTypeCode = FeeComponentValidatorHelper.NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = FeeComponentValidatorHelper.NormalizeCode(request.CalculationTypeCode);

        Result validation = await FeeComponentValidatorHelper.ValidateAsync(
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
        string componentTypeCode = FeeComponentValidatorHelper.NormalizeCode(request.ComponentTypeCode);
        string calculationTypeCode = FeeComponentValidatorHelper.NormalizeCode(request.CalculationTypeCode);

        Result validation = await FeeComponentValidatorHelper.ValidateAsync(
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

        await feeComponents.SaveAsync(feeComponent, cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }
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
        await feeComponents.SaveAsync(feeComponent, cancellationToken);
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
        await feeComponents.SaveAsync(feeComponent, cancellationToken);
        return Result<FeeComponentDto>.Success(FeeComponentMapper.ToDto(feeComponent));
    }
}
