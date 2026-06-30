using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

public sealed record PreviewCoursePaymentPlanBillQuery(
    long CourseId,
    long PaymentPlanId,
    IReadOnlyCollection<long>? FasApplicationSchemeIds = null)
    : IQuery<PaymentPlanBillPreviewResponse>;

internal sealed class PreviewCoursePaymentPlanBillHandler(
    ICourseEnrollmentRepository enrollments,
    IStudentAccessControl studentAccess,
    IClock clock,
    PaymentPlanBillPreviewBuilder previewBuilder)
    : IQueryHandler<PreviewCoursePaymentPlanBillQuery, PaymentPlanBillPreviewResponse>
{
    public async Task<Result<PaymentPlanBillPreviewResponse>> Handle(
        PreviewCoursePaymentPlanBillQuery query,
        CancellationToken ct)
    {
        long? personId = studentAccess.PersonId;
        if (personId is null || !studentAccess.IsStudent)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
        }

        Course? course = await enrollments.FindCourseAsync(query.CourseId, ct);
        if (course is null)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.CourseNotFound);
        }

        if (course.IsDisabled)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseErrors.CourseDisabled);
        }

        if (!course.IsPublished)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseErrors.CourseNotPublished);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        if (utcNow < course.EnrollmentOpenAtUtc || utcNow > course.EnrollmentCloseAtUtc)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseErrors.EnrollmentWindowClosed);
        }

        if (!await studentAccess.CanUseSchoolServiceAsync(course.OrganizationId, ct))
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.PersonNotInCourseOrganization);
        }

        if (await enrollments.ExistsAsync(personId.Value, query.CourseId, ct))
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.DuplicateEnrollment);
        }

        return await previewBuilder.BuildAsync(
            0,
            personId.Value,
            query.CourseId,
            query.PaymentPlanId,
            query.FasApplicationSchemeIds,
            ct);
    }
}

internal sealed class PaymentPlanBillPreviewBuilder(
    ICourseEnrollmentRepository enrollments,
    ICoursePaymentPlanGateway plans,
    IFasCourseSubsidyGateway fasSubsidies,
    IClock clock)
{
    public async Task<Result<PaymentPlanBillPreviewResponse>> BuildAsync(
        long courseEnrollmentId,
        long personId,
        long courseId,
        long paymentPlanId,
        IReadOnlyCollection<long>? fasApplicationSchemeIds,
        CancellationToken ct)
    {
        CourseBillingPlan? plan = await plans.FindPlanAsync(paymentPlanId, ct);
        if (plan is null || !plan.IsActive || plan.CourseId != courseId)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.PaymentPlanNotFound);
        }

        IReadOnlyCollection<CourseFeeBillingLine> fees =
            await enrollments.ListActiveCourseFeesAsync(courseId, ct);
        if (fees.Count == 0)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
        }

        DateTime now = clock.UtcNow.UtcDateTime;
        bool installment = plan.PlanTypeCode == "INSTALLMENT";
        DateOnly dueDate = installment
            ? new DateOnly(now.Year, now.Month, 1).AddMonths(1)
            : DateOnly.FromDateTime(now);

        IReadOnlyCollection<CourseFasSubsidy> selectedFasSubsidies =
            await fasSubsidies.ListEligibleSubsidiesAsync(
                personId,
                courseId,
                DateOnly.FromDateTime(now),
                fasApplicationSchemeIds,
                ct);
        int requestedFasCount = fasApplicationSchemeIds?.Where(id => id > 0).Distinct().Count() ?? 0;
        if (selectedFasSubsidies.Count != requestedFasCount)
        {
            return Result<PaymentPlanBillPreviewResponse>.Failure(CourseBillingErrors.FasVoucherUnavailable);
        }

        CourseEnrollmentBillingPreviewResult preview = enrollments.PreviewPaymentPlanBills(
            plan,
            installment,
            dueDate,
            fees,
            selectedFasSubsidies);

        return Result<PaymentPlanBillPreviewResponse>.Success(Map(courseEnrollmentId, plan, preview));
    }

    private static PaymentPlanBillPreviewResponse Map(
        long courseEnrollmentId,
        CourseBillingPlan plan,
        CourseEnrollmentBillingPreviewResult preview)
    {
        PaymentPlanPreviewBillResponse[] bills = preview.Bills.Select(x => new PaymentPlanPreviewBillResponse(
            0,
            $"PREVIEW-{x.SequenceNumber:D2}",
            x.SequenceNumber,
            x.CurrentDueDate,
            x.CurrentDueDate,
            x.GrossAmount,
            x.SubsidyAmount,
            x.NetPayableAmount,
            x.NetPayableAmount,
            "PREVIEW",
            plan.PlanTypeCode,
            x.IsInstallment,
            x.IsInstallment,
            x.IsInstallment ? null : "Full payment bills cannot be deferred.",
            x.Lines.Select(line => new PaymentPlanPreviewBillLineResponse(
                0,
                line.FeeComponentId,
                line.CourseFeeId,
                line.ComponentCode,
                line.ComponentName,
                line.ComponentTypeCode,
                line.CalculationTypeCode,
                line.Description,
                1m,
                line.GrossAmount,
                line.GrossAmount,
                line.SubsidyAmount,
                line.NetAmount)).ToArray())).ToArray();

        return new(
            courseEnrollmentId,
            plan.CoursePaymentPlanId,
            plan.PlanTypeCode,
            plan.InstallmentCount,
            preview.GrossAmount,
            preview.SubsidyAmount,
            preview.NetPayableAmount,
            bills);
    }
}
