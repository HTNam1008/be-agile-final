using FluentValidation;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.EnrollmentCancellations;

internal sealed record CancelEnrollmentCommand(long EnrollmentId, CancelEnrollmentRequest Request)
    : ICommand<EnrollmentCancellationResponse>;

internal sealed class CancelEnrollmentRequestValidator : AbstractValidator<CancelEnrollmentRequest>
{
    public CancelEnrollmentRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(128);
    }
}

internal sealed class CancelEnrollmentHandler(
    ICurrentUser currentUser,
    IEnrollmentRefundPreviewRepository previews,
    IEnrollmentRefundProcessor refunds,
    IEnrollmentCancellationRepository cancellations,
    IStudentDirectory students,
    ISchoolAdminNotificationRecipientResolver schoolAdminRecipients,
    INotificationWriter notificationWriter,
    IClock clock)
    : ICommandHandler<CancelEnrollmentCommand, EnrollmentCancellationResponse>
{
    public async Task<Result<EnrollmentCancellationResponse>> Handle(
        CancelEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.TryGetStudent(out long personId) || currentUser.UserAccountId is not long actorId)
            return Result<EnrollmentCancellationResponse>.Failure(PaymentApplicationErrors.StudentRequired);

        string idempotencyKey = command.Request.IdempotencyKey.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result<EnrollmentCancellationResponse>.Failure(PaymentApplicationErrors.IdempotencyKeyRequired);

        EnrollmentCancellationSnapshot? snapshot = await previews.FindAsync(
            command.EnrollmentId,
            personId,
            cancellationToken);
        if (snapshot is null)
            return Result<EnrollmentCancellationResponse>.Failure(PaymentApplicationErrors.EnrollmentNotFound);

        DateTime now = clock.UtcNow.UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(now);
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

        if (!calculation.CanCancel)
            return Result<EnrollmentCancellationResponse>.Failure(EnrollmentCancellationPolicy.ToError(calculation));

        EnrollmentRefundExecutionResult? refundResult = null;
        if (calculation.RefundAmount > 0m)
        {
            Result<EnrollmentRefundExecutionResult> refund = await refunds.ExecuteAsync(
                snapshot,
                calculation,
                idempotencyKey,
                actorId,
                cancellationToken);
            if (refund.IsFailure)
                return Result<EnrollmentCancellationResponse>.Failure(refund.Error);

            refundResult = refund.Value;
        }

        Result<string> cancellation = await cancellations.CancelEnrollmentAndOutstandingBillsAsync(
            snapshot.Enrollment.Id,
            personId,
            calculation.RefundAmount > 0m,
            now,
            cancellationToken);
        if (cancellation.IsFailure)
            return Result<EnrollmentCancellationResponse>.Failure(cancellation.Error);

        await NotifyCourseExitAsync(
            snapshot.Course.OrganizationId,
            snapshot.Enrollment.PersonId,
            snapshot.Course.CourseName,
            cancellationToken);

        return Result<EnrollmentCancellationResponse>.Success(new(
            snapshot.Enrollment.Id,
            snapshot.Course.Id,
            snapshot.Course.CourseCode,
            snapshot.Course.CourseName,
            true,
            cancellation.Value,
            refundResult?.EnrollmentRefundId,
            refundResult?.RefundStatusCode,
            snapshot.PaidAmount,
            calculation.RefundAmount,
            calculation.EducationAccountRefundAmount,
            calculation.OnlineRefundAmount,
            now));
    }

    private async Task NotifyCourseExitAsync(
        long organizationId,
        long personId,
        string courseName,
        CancellationToken cancellationToken)
    {
        StudentSummary? student = await students.FindByPersonIdAsync(personId, cancellationToken);
        string studentName = student?.DisplayName ?? $"Student {personId}";

        IReadOnlyCollection<long> userAccountIds = await schoolAdminRecipients.FindUserAccountIdsByOrganizationIdAsync(
            organizationId,
            cancellationToken);

        foreach (long userAccountId in userAccountIds.Distinct())
        {
            await notificationWriter.CreateAsync(
                new NotificationCreateRequest(
                    userAccountId,
                    NotificationTypeCode.CourseExit,
                    "Course Exit",
                    $"Result: STUDENT_CANCELLED. {studentName} cancelled enrollment in {courseName}."),
                cancellationToken);
        }
    }
}
