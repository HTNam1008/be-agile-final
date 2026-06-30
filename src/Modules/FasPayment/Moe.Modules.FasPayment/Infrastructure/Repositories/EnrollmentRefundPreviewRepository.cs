using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class EnrollmentRefundPreviewRepository(MoeDbContext dbContext)
    : IEnrollmentRefundPreviewRepository
{
    public async Task<EnrollmentCancellationSnapshot?> FindAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken)
    {
        CourseEnrollment? enrollment = await dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == enrollmentId && x.PersonId == personId,
                cancellationToken);
        if (enrollment is null)
            return null;

        Course? course = await dbContext.Set<Course>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == enrollment.CourseId, cancellationToken);
        if (course is null)
            return null;

        var billSnapshots = await dbContext.Set<Bill>()
            .AsNoTracking()
            .Where(x => x.CourseEnrollmentId == enrollment.Id)
            .Select(x => new
            {
                x.Id,
                x.BillStatusCode,
                x.OutstandingAmount
            })
            .ToArrayAsync(cancellationToken);
        long[] billIds = billSnapshots.Select(x => x.Id).ToArray();
        decimal outstandingAmount = Money(billSnapshots
            .Where(x => x.BillStatusCode is not (BillStatusCodes.Paid or BillStatusCodes.Cancelled))
            .Sum(x => x.OutstandingAmount));
        int outstandingBillCount = billSnapshots.Count(x =>
            x.BillStatusCode is not (BillStatusCodes.Paid or BillStatusCodes.Cancelled) &&
            x.OutstandingAmount > 0m);

        var rows = await (
            from allocation in dbContext.Set<PaymentAllocation>().AsNoTracking()
            join payment in dbContext.Set<Payment>().AsNoTracking()
                on allocation.PaymentId equals payment.Id
            join checkout in dbContext.Set<PaymentCheckoutSession>().AsNoTracking()
                on payment.Id equals checkout.PaymentId into checkoutRows
            from checkout in checkoutRows.DefaultIfEmpty()
            where billIds.Contains(allocation.BillId)
                && allocation.AllocationStatusCode == "APPLIED"
                && (payment.PaymentStatusCode == PaymentStatusCodes.Successful
                    || payment.PaymentStatusCode == PaymentStatusCodes.PartiallyRefunded)
            select new
            {
                allocation.AllocatedAmount,
                payment.Id,
                payment.PaymentAmount,
                payment.EducationAccountAmount,
                payment.OnlinePaymentAmount,
                payment.ProviderChargeId,
                ProviderPaymentIntentId = payment.ProviderPaymentIntentId ?? checkout.ProviderPaymentIntentId
            }).ToArrayAsync(cancellationToken);

        decimal paid = 0m;
        decimal educationPaid = 0m;
        decimal onlinePaid = 0m;
        List<EnrollmentPaymentRefundSource> sources = [];
        foreach (var row in rows)
        {
            if (row.PaymentAmount <= 0m)
                continue;

            decimal educationAllocated = Money(
                row.AllocatedAmount * row.EducationAccountAmount / row.PaymentAmount);
            decimal onlineAllocated = Money(
                row.AllocatedAmount * row.OnlinePaymentAmount / row.PaymentAmount);
            PaymentPart? educationPart = row.EducationAccountAmount > 0m
                ? await dbContext.Set<PaymentPart>().AsNoTracking()
                    .SingleOrDefaultAsync(
                        x => x.PaymentId == row.Id
                            && x.PaymentMethodCode == PaymentMethodCodes.EducationAccount,
                        cancellationToken)
                : null;

            paid += row.AllocatedAmount;
            educationPaid += educationAllocated;
            onlinePaid += onlineAllocated;
            sources.Add(new(
                row.Id,
                educationPart?.Id,
                educationPart?.AccountTransactionId,
                row.ProviderChargeId ?? row.ProviderPaymentIntentId,
                Money(row.AllocatedAmount),
                educationAllocated,
                onlineAllocated));
        }

        return new(
            enrollment,
            course,
            Money(paid),
            Money(educationPaid),
            Money(onlinePaid),
            outstandingAmount,
            outstandingBillCount,
            sources);
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
