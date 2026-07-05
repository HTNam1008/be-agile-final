using FluentAssertions;
using Moe.Modules.FasPayment.Domain.Payments;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class CoursePaymentPlanPolicyTests
{
    [Fact]
    public void AllowedInstallmentCounts_ShouldIncludeSupportedMonthlyPlans()
    {
        CoursePaymentPlanPolicy.AllowedInstallmentCounts.Should().Equal(2, 3, 6, 9, 12);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    public void Create_ShouldAllowConfiguredInstallmentCounts(int installmentCount)
    {
        var result = CoursePaymentPlan.Create(
            1,
            $"{installmentCount} monthly installments",
            PaymentPlanTypeCodes.Installment,
            installmentCount,
            1,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        result.IsSuccess.Should().BeTrue();
        result.Value.InstallmentCount.Should().Be(installmentCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(24)]
    public void Create_ShouldRejectInstallmentCountsOutsidePolicy(int installmentCount)
    {
        var result = CoursePaymentPlan.Create(
            1,
            $"{installmentCount} monthly installments",
            PaymentPlanTypeCodes.Installment,
            installmentCount,
            1,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        result.IsFailure.Should().BeTrue();
    }
}
