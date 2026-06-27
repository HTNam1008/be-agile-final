using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.AdminEnrollPerson;

internal sealed class AdminEnrollPersonHandler(
    ICourseEnrollmentRepository enrollments,
    ICoursePaymentPlanGateway paymentPlans,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ICommandHandler<AdminEnrollPersonCommand, CourseEnrollmentResponse>
{
    public async Task<Result<CourseEnrollmentResponse>> Handle(
        AdminEnrollPersonCommand command,
        CancellationToken cancellationToken)
    {
        long? actorId = currentUser.UserAccountId;

        if (actorId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.ActorRequired);
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

        DateTime utcNow = clock.UtcNow.UtcDateTime;

        if (utcNow < course.EnrollmentOpenAtUtc || utcNow > course.EnrollmentCloseAtUtc)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseErrors.EnrollmentWindowClosed);
        }

        if (!adminAccess.CanAccessOrganization(course.OrganizationId))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseOrganizationForbidden);
        }

        DateOnly today = DateOnly.FromDateTime(utcNow);
        long? personId = await enrollments.FindActiveStudentPersonIdAsync(
            command.StudentNumber,
            course.OrganizationId,
            today,
            cancellationToken);

        if (personId is null)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotFound);
        }

        if (!await enrollments.PersonHasActiveSchoolEnrollmentAsync(
            personId.Value,
            course.OrganizationId,
            today,
            cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(personId.Value, command.CourseId, cancellationToken))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        CourseBillingPlan? plan = null;
        if (command.CoursePaymentPlanId is long coursePaymentPlanId)
        {
            plan = await paymentPlans.FindPlanAsync(
                coursePaymentPlanId,
                cancellationToken);
            if (plan is null || !plan.IsActive || plan.CourseId != command.CourseId)
                return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PaymentPlanNotFound);
        }

        Result<CourseEnrollment> enrollmentResult = plan is null
            ? CourseEnrollment.EnrollByAdminPendingPlanSelection(
                personId.Value,
                command.CourseId,
                actorId.Value,
                utcNow,
                course.BeforeStartRefundPercentage,
                course.AfterStartRefundPercentage)
            : CourseEnrollment.EnrollByAdmin(
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

        if (course.IsDraft || plan is null)
        {
            await enrollments.AddEnrollmentAsync(enrollmentResult.Value, cancellationToken);
            await RecordEnrollmentAuditAsync(course, enrollmentResult.Value, cancellationToken);
            return Result<CourseEnrollmentResponse>.Success(ToPendingResponse(enrollmentResult.Value));
        }

        IReadOnlyCollection<CourseFeeBillingLine> feeLines = await enrollments.ListActiveCourseFeesAsync(
            command.CourseId,
            cancellationToken);

        if (feeLines.Count == 0)
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
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
            [],
            cancellationToken);

        await RecordEnrollmentAuditAsync(course, billingResult.Enrollment, cancellationToken);
        return Result<CourseEnrollmentResponse>.Success(ToResponse(billingResult));
    }

    private async Task RecordEnrollmentAuditAsync(
        Course course,
        CourseEnrollment enrollment,
        CancellationToken cancellationToken)
    {
        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.CourseEnrollmentCreatedByAdmin,
                "CourseEnrollment",
                enrollment.Id,
                course.OrganizationId,
                new SchoolAuditDetails(
                    "Student manually enrolled into course",
                    EntityDisplayName: course.CourseName,
                    RelatedIds: new Dictionary<string, long>
                    {
                        ["studentPersonId"] = enrollment.PersonId,
                        ["courseId"] = course.Id
                    })),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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

    private static CourseEnrollmentResponse ToPendingResponse(CourseEnrollment enrollment)
        => new(
            enrollment.Id,
            enrollment.PersonId,
            enrollment.CourseId,
            enrollment.EnrollmentSourceCode,
            enrollment.EnrolledByLoginAccountId,
            enrollment.EnrollmentStatusCode,
            null,
            null,
            null,
            0,
            0m,
            0m,
            0m,
            []);

    private static string CreateBillNumber(DateTime utcNow)
        => $"BILL-{utcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
}
