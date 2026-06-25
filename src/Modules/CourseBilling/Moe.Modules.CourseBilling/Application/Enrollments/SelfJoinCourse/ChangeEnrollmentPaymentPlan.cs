using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;

public sealed record ChangeEnrollmentPaymentPlanCommand(
    long EnrollmentId,
    long PaymentPlanId,
    IReadOnlyCollection<long>? FasApplicationSchemeIds = null)
    : ICommand<CourseEnrollmentResponse>;

internal sealed class ChangeEnrollmentPaymentPlanHandler(
    ICourseEnrollmentRepository enrollments,
    ICoursePaymentPlanGateway plans,
    IFasCourseSubsidyGateway fasSubsidies,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<ChangeEnrollmentPaymentPlanCommand, CourseEnrollmentResponse>
{
    public async Task<Result<CourseEnrollmentResponse>> Handle(
        ChangeEnrollmentPaymentPlanCommand command,
        CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? 0;
        if (!currentUser.IsAuthenticated || personId <= 0)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.StudentIdentityRequired);
        CourseEnrollment? enrollment = await enrollments.FindEnrollmentAsync(command.EnrollmentId, personId, ct);
        if (enrollment is null)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseNotFound);
        if (enrollment.EnrollmentStatusCode is not (
            CourseEnrollmentStatusCodes.PendingPlanSelection or
            CourseEnrollmentStatusCodes.PendingPayment or
            CourseEnrollmentStatusCodes.PaymentPastDue))
        {
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PaymentPlanChangeNotAllowed);
        }
        CourseBillingPlan? plan = await plans.FindPlanAsync(command.PaymentPlanId, ct);
        if (plan is null || !plan.IsActive || plan.CourseId != enrollment.CourseId)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PaymentPlanNotFound);
        IReadOnlyCollection<CourseFeeBillingLine> fees =
            await enrollments.ListActiveCourseFeesAsync(enrollment.CourseId, ct);
        if (fees.Count == 0)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.CourseFeesNotConfigured);
        DateTime now = clock.UtcNow.UtcDateTime;
        bool installment = plan.PlanTypeCode == "INSTALLMENT";
        DateOnly dueDate = installment
            ? new DateOnly(now.Year, now.Month, 1).AddMonths(1)
            : DateOnly.FromDateTime(now);
        IReadOnlyCollection<CourseFasSubsidy> selectedFasSubsidies =
            await fasSubsidies.ListEligibleSubsidiesAsync(
                personId,
                enrollment.CourseId,
                DateOnly.FromDateTime(now),
                command.FasApplicationSchemeIds,
                ct);
        CourseEnrollmentBillingResult? result =
            await enrollments.ChangePaymentPlanAndReissueBillsAsync(
                enrollment, plan.CoursePaymentPlanId, installment,
                $"BILL-{now:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant(),
                now, dueDate, plan.InstallmentCount, plan.IntervalMonths, fees, selectedFasSubsidies, ct);
        if (result is null)
            return Result<CourseEnrollmentResponse>.Failure(CourseBillingErrors.PaymentPlanChangeNotAllowed);
        GeneratedBillResult first = result.Bills.OrderBy(x => x.Bill.SequenceNumber).First();
        await fasSubsidies.RecordPendingRedemptionsAsync(
            personId,
            enrollment.CourseId,
            enrollment.Id,
            first.Bill.Id,
            result.Bills.Sum(x => x.Bill.SubsidyAmount),
            selectedFasSubsidies,
            now,
            ct);
        long[] paidBillIds = result.Bills
            .Where(x => x.Bill.BillStatusCode == BillStatusCodes.Paid)
            .Select(x => x.Bill.Id)
            .ToArray();
        if (paidBillIds.Length > 0)
        {
            await fasSubsidies.RedeemPendingRedemptionsForBillsAsync(
                paidBillIds,
                now,
                ct);
        }
        return Result<CourseEnrollmentResponse>.Success(new(
            enrollment.Id, enrollment.PersonId, enrollment.CourseId,
            enrollment.EnrollmentSourceCode, enrollment.EnrolledByLoginAccountId,
            enrollment.EnrollmentStatusCode, first.Bill.Id, first.Bill.BillNumber,
            first.Bill.BillStatusCode, result.Bills.Sum(x => x.BillLineCount),
            result.Bills.Sum(x => x.Bill.GrossAmount), result.Bills.Sum(x => x.Bill.NetPayableAmount),
            result.Bills.Sum(x => x.Bill.OutstandingAmount),
            result.Bills.Select(x => new GeneratedEnrollmentBillResponse(
                x.Bill.Id, x.Bill.BillNumber, x.Bill.SequenceNumber,
                x.Bill.CurrentDueDate, x.Bill.NetPayableAmount,
                x.Bill.OutstandingAmount, x.Bill.BillStatusCode)).ToArray()));
    }
}
