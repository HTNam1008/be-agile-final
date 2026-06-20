using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal static class FeeComponentMapper
{
    public static FeeComponentDto ToDto(FeeComponent feeComponent)
        => new(
            feeComponent.Id,
            feeComponent.ComponentCode,
            feeComponent.ComponentName,
            feeComponent.ComponentTypeCode,
            feeComponent.CalculationTypeCode,
            feeComponent.IsTaxComponent,
            feeComponent.IsActive);
}
