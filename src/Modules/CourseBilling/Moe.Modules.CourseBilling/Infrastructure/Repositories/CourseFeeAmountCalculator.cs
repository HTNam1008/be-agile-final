using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal static class CourseFeeAmountCalculator
{
    public static IReadOnlyList<CourseFeeBillingAmount> Calculate(IReadOnlyCollection<CourseFeeBillingLine> feeLines)
    {
        decimal subtotal = feeLines
            .Where(line => !line.IsTaxComponent)
            .Sum(line => ResolveFixedAmount(line));

        return feeLines
            .Select(line => new CourseFeeBillingAmount(
                line.CourseFeeId,
                line.FeeComponentId,
                line.FeeComponentName,
                CalculateAmount(line, subtotal)))
            .ToArray();
    }

    public static IReadOnlyList<CourseFeeBillingAmount> Calculate(IReadOnlyCollection<CourseFeeDetail> fees)
    {
        decimal subtotal = fees
            .Where(detail => !detail.FeeComponent.IsTaxComponent)
            .Sum(detail => ResolveFixedAmount(detail.CourseFee.FeeValue, detail.FeeComponent.CalculationTypeCode));

        return fees
            .Select(detail => new CourseFeeBillingAmount(
                detail.CourseFee.Id,
                detail.FeeComponent.Id,
                detail.FeeComponent.ComponentName,
                CalculateAmount(
                    detail.CourseFee.FeeValue,
                    detail.FeeComponent.CalculationTypeCode,
                    detail.FeeComponent.IsTaxComponent,
                    subtotal)))
            .ToArray();
    }

    public static IReadOnlyList<CourseFeeBillingAmount> AllocateInstallment(
        IReadOnlyList<CourseFeeBillingAmount> totalAmounts,
        int sequence,
        int installmentCount)
    {
        return totalAmounts
            .Select(amount => amount with
            {
                Amount = AllocateBySequence(amount.Amount, sequence, installmentCount)
            })
            .ToArray();
    }

    private static decimal CalculateAmount(CourseFeeBillingLine line, decimal subtotal)
        => CalculateAmount(line.FeeValue, line.CalculationTypeCode, line.IsTaxComponent, subtotal);

    private static decimal CalculateAmount(
        decimal feeValue,
        string calculationTypeCode,
        bool isTaxComponent,
        decimal subtotal)
    {
        if (isTaxComponent &&
            string.Equals(calculationTypeCode, FeeComponentCalculationTypes.Percentage, StringComparison.OrdinalIgnoreCase))
        {
            return Money(subtotal * feeValue / 100m);
        }

        return ResolveFixedAmount(feeValue, calculationTypeCode);
    }

    private static decimal ResolveFixedAmount(CourseFeeBillingLine line)
        => ResolveFixedAmount(line.FeeValue, line.CalculationTypeCode);

    private static decimal ResolveFixedAmount(decimal feeValue, string calculationTypeCode)
    {
        return string.Equals(calculationTypeCode, FeeComponentCalculationTypes.Percentage, StringComparison.OrdinalIgnoreCase)
            ? 0m
            : Money(feeValue);
    }

    private static decimal AllocateBySequence(decimal amount, int sequence, int installmentCount)
    {
        long totalMinor = decimal.ToInt64(Money(amount) * 100m);
        long baseMinor = totalMinor / installmentCount;
        long remainderMinor = totalMinor % installmentCount;
        return (baseMinor + (sequence <= remainderMinor ? 1 : 0)) / 100m;
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

internal sealed record CourseFeeBillingAmount(
    long CourseFeeId,
    long FeeComponentId,
    string FeeComponentName,
    decimal Amount);
