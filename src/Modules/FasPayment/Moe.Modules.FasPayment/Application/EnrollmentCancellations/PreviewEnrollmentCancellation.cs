using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed record PreviewEnrollmentCancellationQuery(long EnrollmentId)
    : IQuery<EnrollmentCancellationPreviewResponse>;

internal sealed class PreviewEnrollmentCancellationHandler(
    ICurrentUser currentUser,
    IEnrollmentRefundPreviewRepository previews,
    IClock clock)
    : IQueryHandler<PreviewEnrollmentCancellationQuery, EnrollmentCancellationPreviewResponse>
{
    public async Task<Result<EnrollmentCancellationPreviewResponse>> Handle(
        PreviewEnrollmentCancellationQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId))
            return Result<EnrollmentCancellationPreviewResponse>.Failure(
                PaymentApplicationErrors.StudentRequired);

        EnrollmentCancellationSnapshot? snapshot = await previews.FindAsync(
            query.EnrollmentId,
            personId,
            cancellationToken);
        if (snapshot is null)
            return Result<EnrollmentCancellationPreviewResponse>.Failure(
                PaymentApplicationErrors.EnrollmentNotFound);

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        EnrollmentRefundCalculation calculation = EnrollmentRefundPreviewCalculator.Calculate(
            today,
            snapshot.Course.StartDate,
            snapshot.Course.EndDate,
            snapshot.Enrollment.BeforeStartRefundPercentage,
            snapshot.Enrollment.AfterStartRefundPercentage,
            new(
                snapshot.PaidAmount,
                snapshot.EducationAccountPaidAmount,
                snapshot.OnlinePaidAmount));
        calculation = EnrollmentCancellationPolicy.Apply(snapshot, calculation, today);

        return Result<EnrollmentCancellationPreviewResponse>.Success(new(
            snapshot.Enrollment.Id,
            snapshot.Course.Id,
            snapshot.Course.CourseCode,
            snapshot.Course.CourseName,
            calculation.CanCancel,
            calculation.PolicyPeriodCode,
            calculation.RefundPercentage,
            snapshot.PaidAmount,
            calculation.RefundAmount,
            calculation.EducationAccountRefundAmount,
            calculation.OnlineRefundAmount,
            snapshot.Course.StartDate,
            snapshot.Course.EndDate,
            calculation.CannotCancelReason));
    }
}
