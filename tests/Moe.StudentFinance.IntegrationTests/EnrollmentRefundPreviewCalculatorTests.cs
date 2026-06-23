using Moe.Modules.FasPayment.Application.EnrollmentCancellations;
using Moe.Modules.FasPayment.Contracts.Payments;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EnrollmentRefundPreviewCalculatorTests
{
    [Fact]
    public void BeforeStart_UsesConfiguredPercentageAndOriginalSources()
    {
        EnrollmentRefundCalculation result = EnrollmentRefundPreviewCalculator.Calculate(
            new DateOnly(2026, 7, 31),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 1),
            80m,
            30m,
            new EnrollmentPaidAmounts(150m, 100m, 50m));

        Assert.True(result.CanCancel);
        Assert.Equal(RefundPolicyPeriodCodes.BeforeCourseStart, result.PolicyPeriodCode);
        Assert.Equal(80m, result.RefundPercentage);
        Assert.Equal(120m, result.RefundAmount);
        Assert.Equal(80m, result.EducationAccountRefundAmount);
        Assert.Equal(40m, result.OnlineRefundAmount);
    }

    [Fact]
    public void DuringCourse_UsesConfiguredPercentageAndRoundsEachSource()
    {
        EnrollmentRefundCalculation result = EnrollmentRefundPreviewCalculator.Calculate(
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 1),
            100m,
            50m,
            new EnrollmentPaidAmounts(100m, 33.33m, 66.67m));

        Assert.True(result.CanCancel);
        Assert.Equal(RefundPolicyPeriodCodes.DuringCourse, result.PolicyPeriodCode);
        Assert.Equal(16.67m, result.EducationAccountRefundAmount);
        Assert.Equal(33.34m, result.OnlineRefundAmount);
        Assert.Equal(50.01m, result.RefundAmount);
    }

    [Fact]
    public void AfterCourseEnd_DisallowsCancellationAndRefund()
    {
        EnrollmentRefundCalculation result = EnrollmentRefundPreviewCalculator.Calculate(
            new DateOnly(2026, 10, 2),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 10, 1),
            100m,
            50m,
            new EnrollmentPaidAmounts(100m, 40m, 60m));

        Assert.False(result.CanCancel);
        Assert.Equal(RefundPolicyPeriodCodes.CourseEnded, result.PolicyPeriodCode);
        Assert.Equal(0m, result.RefundAmount);
    }
}
