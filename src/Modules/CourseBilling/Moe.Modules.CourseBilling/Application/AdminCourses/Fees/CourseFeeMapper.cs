using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.IGateway.Repositories;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Fees;

internal static class CourseFeeMapper
{
    public static CourseFeeDto ToFeeDto(CourseFeeDetail detail)
        => new(
            detail.CourseFee.Id,
            detail.CourseFee.CourseId,
            detail.CourseFee.FeeComponentId,
            detail.FeeComponent.ComponentCode,
            detail.FeeComponent.ComponentName,
            detail.FeeComponent.ComponentTypeCode,
            detail.FeeComponent.CalculationTypeCode,
            detail.CourseFee.FeeValue,
            detail.CourseFee.SequenceNumber,
            detail.FeeComponent.IsSystemManaged,
            detail.CourseFee.IsActive);
}
