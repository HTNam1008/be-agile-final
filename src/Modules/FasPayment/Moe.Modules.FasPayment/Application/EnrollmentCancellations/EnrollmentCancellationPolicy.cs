using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal static class EnrollmentCancellationPolicy
{
    public const string OutstandingBillsReason =
        "This course has started and has outstanding bills. Please settle the bills or contact your school admin.";

    public static EnrollmentRefundCalculation Apply(
        EnrollmentCancellationSnapshot snapshot,
        EnrollmentRefundCalculation calculation,
        DateOnly today)
    {
        if (!calculation.CanCancel)
            return calculation;

        if (today >= snapshot.Course.StartDate &&
            snapshot.OutstandingAmount > 0m &&
            snapshot.OutstandingBillCount > 0)
        {
            return calculation with
            {
                CanCancel = false,
                CannotCancelReason = OutstandingBillsReason
            };
        }

        return calculation;
    }

    public static Error ToError(EnrollmentRefundCalculation calculation)
        => string.Equals(calculation.CannotCancelReason, OutstandingBillsReason, StringComparison.Ordinal)
            ? PaymentApplicationErrors.CancellationOutstandingBillsRequired
            : PaymentApplicationErrors.CancellationNotAllowed;
}
