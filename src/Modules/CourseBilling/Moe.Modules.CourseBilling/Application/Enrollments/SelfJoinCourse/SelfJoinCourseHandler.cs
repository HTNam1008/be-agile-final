using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

internal sealed class SelfJoinCourseHandler(
    ICourseEnrollmentRepository enrollments,
    ICoursePaymentPlanGateway paymentPlans,
    ICurrentUser currentUser,
    IStudentAccessControl studentAccess,
    IClock clock) : ICommandHandler<SelfJoinCourseCommand, CourseEnrollmentResponse>
{
    public async Task<Result<CourseEnrollmentResponse>> Handle(
        SelfJoinCourseCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
        }

        long? personId = studentAccess.PersonId;

        if (personId is null || !studentAccess.IsStudent)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
        }

        Course? course = await enrollments.FindCourseAsync(command.CourseId, cancellationToken);

        if (course is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseNotFound);
        }

        if (course.IsDisabled)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.CourseDisabled);
        }

        if (!course.IsPublished)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.CourseNotPublished);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;

        if (utcNow < course.EnrollmentOpenAtUtc || utcNow > course.EnrollmentCloseAtUtc)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.EnrollmentWindowClosed);
        }

        if (!await studentAccess.CanUseSchoolServiceAsync(course.OrganizationId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(personId.Value, command.CourseId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        CourseBillingPlan? plan = await paymentPlans.FindPlanAsync(
            command.CoursePaymentPlanId,
            cancellationToken);
        if (plan is null || !plan.IsActive || plan.CourseId != command.CourseId)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PaymentPlanNotFound);

        IReadOnlyCollection<CourseFeeBillingLine> feeLines = await enrollments.ListActiveCourseFeesAsync(
            command.CourseId,
            cancellationToken);

        if (feeLines.Count == 0)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
        }

        Result<CourseEnrollment> enrollmentResult = CourseEnrollment.JoinSelf(
            personId.Value,
            command.CourseId,
            plan.CoursePaymentPlanId,
            actorId.Value,
            utcNow,
            course.BeforeStartRefundPercentage,
            course.AfterStartRefundPercentage);

        if (enrollmentResult.IsFailure)
        {
            return Result<CourseEnrollmentResponse>.Failure(enrollmentResult.Error);
        }

        bool installment = plan.PlanTypeCode == "INSTALLMENT";
        if (installment)
            enrollmentResult.Value.ActivateInstallmentEnrollment();

        DateOnly enrolledDate = DateOnly.FromDateTime(utcNow);
        DateOnly firstDueDate = installment
            ? new DateOnly(enrolledDate.Year, enrolledDate.Month, 1).AddMonths(1)
            : enrolledDate;
        CourseEnrollmentBillingResult billingResult = await enrollments.AddEnrollmentAndIssueBillsAsync(
            enrollmentResult.Value,
            CreateBillNumber(utcNow),
            utcNow,
            firstDueDate,
            plan.InstallmentCount,
            plan.IntervalMonths,
            feeLines,
            cancellationToken);

        return Result<CourseEnrollmentResponse>.Success(ToResponse(billingResult));
    }

    private static CourseEnrollmentResponse ToResponse(CourseEnrollmentBillingResult result)
    {
        GeneratedBillResult first = result.Bills.OrderBy(x => x.Bill.SequenceNumber).First();
        return new(
            result.Enrollment.Id,
            result.Enrollment.PersonId,
            result.Enrollment.CourseId,
            result.Enrollment.EnrollmentSourceCode,
            result.Enrollment.EnrolledByLoginAccountId,
            result.Enrollment.EnrollmentStatusCode,
            first.Bill.Id,
            first.Bill.BillNumber,
            first.Bill.BillStatusCode,
            result.Bills.Sum(x => x.BillLineCount),
            result.Bills.Sum(x => x.Bill.GrossAmount),
            result.Bills.Sum(x => x.Bill.NetPayableAmount),
            result.Bills.Sum(x => x.Bill.OutstandingAmount),
            result.Bills.Select(x => new GeneratedEnrollmentBillResponse(
                x.Bill.Id,
                x.Bill.BillNumber,
                x.Bill.SequenceNumber,
                x.Bill.CurrentDueDate,
                x.Bill.NetPayableAmount,
                x.Bill.OutstandingAmount,
                x.Bill.BillStatusCode)).ToArray());
    }

    private static string CreateBillNumber(DateTime utcNow)
        => $"BILL-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
}
