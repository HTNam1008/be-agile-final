using FluentAssertions;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class FasBillingCalculatorTests
{
    [Fact]
    public void Calculate_ComputesGstOnSubtotalAfterFasDiscounts()
    {
        CourseFeeBillingLine[] feeLines =
        [
            new(1, 10, "Tuition", FeeComponentCalculationTypes.Fixed, false, 1000m),
            new(2, 20, "GST", FeeComponentCalculationTypes.Percentage, true, 9m)
        ];
        CourseFasSubsidy[] subsidies =
        [
            new(100, "PERCENTAGE", 10m),
            new(101, "PERCENTAGE", 10m)
        ];

        FasBillingCalculation calculation = FasBillingCalculator.Calculate(feeLines, subsidies);

        calculation.SubsidyAmount.Should().Be(200m);
        calculation.Amounts.Single(x => x.FeeComponentName == "Tuition").Amount.Should().Be(1000m);
        calculation.Amounts.Single(x => x.FeeComponentName == "GST").Amount.Should().Be(72m);
        calculation.Amounts.Sum(x => x.Amount).Should().Be(1072m);
        (calculation.Amounts.Sum(x => x.Amount) - calculation.SubsidyAmount).Should().Be(872m);
    }

    [Fact]
    public void Calculate_CapsFixedFasDiscountAtRemainingSubtotal()
    {
        CourseFeeBillingLine[] feeLines =
        [
            new(1, 10, "Tuition", FeeComponentCalculationTypes.Fixed, false, 100m),
            new(2, 20, "GST", FeeComponentCalculationTypes.Percentage, true, 9m)
        ];
        CourseFasSubsidy[] subsidies =
        [
            new(100, "FIXED", 120m)
        ];

        FasBillingCalculation calculation = FasBillingCalculator.Calculate(feeLines, subsidies);

        calculation.SubsidyAmount.Should().Be(100m);
        calculation.Amounts.Single(x => x.FeeComponentName == "GST").Amount.Should().Be(0m);
        (calculation.Amounts.Sum(x => x.Amount) - calculation.SubsidyAmount).Should().Be(0m);
    }
}
