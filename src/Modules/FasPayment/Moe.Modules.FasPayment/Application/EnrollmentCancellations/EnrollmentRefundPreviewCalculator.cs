using Moe.Modules.FasPayment.Contracts.Payments;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed record EnrollmentPaidAmounts(
    decimal TotalAmount,
    decimal EducationAccountAmount,
    decimal OnlineAmount);

internal sealed record EnrollmentRefundCalculation(
    bool CanCancel,
    string PolicyPeriodCode,
    decimal RefundPercentage,
    decimal RefundAmount,
    decimal EducationAccountRefundAmount,
    decimal OnlineRefundAmount,
    string? CannotCancelReason);

internal static class EnrollmentRefundPreviewCalculator
{
    public static EnrollmentRefundCalculation Calculate(
        DateOnly today,
        DateOnly courseStartDate,
        DateOnly courseEndDate,
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage,
        EnrollmentPaidAmounts paid)
    {
        if (today > courseEndDate)
        {
            return new(
                false,
                RefundPolicyPeriodCodes.CourseEnded,
                0m,
                0m,
                0m,
                0m,
                "The course has ended and can no longer be cancelled.");
        }

        bool beforeStart = today < courseStartDate;
        decimal percentage = beforeStart
            ? beforeStartRefundPercentage
            : afterStartRefundPercentage;
        decimal educationRefund = Money(paid.EducationAccountAmount * percentage / 100m);
        decimal onlineRefund = Money(paid.OnlineAmount * percentage / 100m);

        return new(
            true,
            beforeStart
                ? RefundPolicyPeriodCodes.BeforeCourseStart
                : RefundPolicyPeriodCodes.DuringCourse,
            percentage,
            educationRefund + onlineRefund,
            educationRefund,
            onlineRefund,
            null);
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
