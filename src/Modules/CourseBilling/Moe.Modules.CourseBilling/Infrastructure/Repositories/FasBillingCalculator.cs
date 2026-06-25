using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Repositories;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal static class FasBillingCalculator
{
    public static FasBillingCalculation Calculate(
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> selectedFas)
    {
        decimal subtotal = Money(feeLines
            .Where(line => !line.IsTaxComponent)
            .Sum(line => ResolveFixedAmount(line.FeeValue, line.CalculationTypeCode)));

        decimal discount = CalculateSubtotalDiscount(subtotal, selectedFas);
        decimal taxableAmount = Money(Math.Max(0m, subtotal - discount));

        CourseFeeBillingAmount[] amounts = feeLines
            .Select(line => new CourseFeeBillingAmount(
                line.CourseFeeId,
                line.FeeComponentId,
                line.FeeComponentName,
                line.IsTaxComponent &&
                string.Equals(line.CalculationTypeCode, FeeComponentCalculationTypes.Percentage, StringComparison.OrdinalIgnoreCase)
                    ? Money(taxableAmount * line.FeeValue / 100m)
                    : ResolveFixedAmount(line.FeeValue, line.CalculationTypeCode)))
            .ToArray();

        return new FasBillingCalculation(amounts, discount);
    }

    private static decimal CalculateSubtotalDiscount(decimal subtotal, IReadOnlyCollection<CourseFasSubsidy> subsidies)
    {
        decimal totalDiscount = 0m;

        foreach (CourseFasSubsidy subsidy in subsidies)
        {
            decimal raw = subsidy.SubsidyTypeCode == "PERCENTAGE"
                ? subtotal * subsidy.SubsidyValue / 100m
                : subsidy.SubsidyValue;
            totalDiscount = Money(totalDiscount + Money(raw));
        }

        return Math.Min(totalDiscount, subtotal);
    }

    private static decimal ResolveFixedAmount(decimal feeValue, string calculationTypeCode)
        => string.Equals(calculationTypeCode, FeeComponentCalculationTypes.Percentage, StringComparison.OrdinalIgnoreCase)
            ? 0m
            : Money(feeValue);

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

internal sealed record FasBillingCalculation(
    IReadOnlyList<CourseFeeBillingAmount> Amounts,
    decimal SubsidyAmount);
