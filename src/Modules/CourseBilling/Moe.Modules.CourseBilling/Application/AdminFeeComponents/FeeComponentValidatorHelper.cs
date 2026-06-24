using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

internal static class FeeComponentValidatorHelper
{
    public static string NormalizeCode(string code)
        => code.Trim().ToUpperInvariant();

    public static async Task<Result> ValidateAsync(
        IAdminFeeComponentRepository feeComponents,
        string componentCode,
        string componentTypeCode,
        string calculationTypeCode,
        decimal defaultValue,
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

        if (defaultValue < 0m ||
            string.Equals(calculationTypeCode, FeeComponentCalculationTypes.Percentage, StringComparison.OrdinalIgnoreCase) &&
            defaultValue > 100m)
        {
            return Result.Failure(CourseErrors.InvalidFeeDefaultValue);
        }

        if (await feeComponents.ComponentCodeExistsAsync(componentCode, excludeFeeComponentId, cancellationToken))
        {
            return Result.Failure(CourseErrors.DuplicateFeeComponentCode);
        }

        return Result.Success();
    }
}
