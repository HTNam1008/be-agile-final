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

        long[] billIds = await dbContext.Set<Bill>()
            .AsNoTracking()
            .Where(x => x.CourseEnrollmentId == enrollment.Id)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);

        var rows = await (
            from allocation in dbContext.Set<PaymentAllocation>().AsNoTracking()
            join payment in dbContext.Set<Payment>().AsNoTracking()
                on allocation.PaymentId equals payment.Id
            where billIds.Contains(allocation.BillId)
                && allocation.AllocationStatusCode == "APPLIED"
                && (payment.PaymentStatusCode == PaymentStatusCodes.Successful
                    || payment.PaymentStatusCode == PaymentStatusCodes.Completed
                    || payment.PaymentStatusCode == PaymentStatusCodes.PartiallyRefunded)
            select new
            {
                allocation.AllocatedAmount,
                payment.PaymentAmount,
                payment.EducationAccountAmount,
                payment.OnlinePaymentAmount
            }).ToArrayAsync(cancellationToken);

        decimal paid = 0m;
        decimal educationPaid = 0m;
        decimal onlinePaid = 0m;
        foreach (var row in rows)
        {
            if (row.PaymentAmount <= 0m)
                continue;

            paid += row.AllocatedAmount;
            educationPaid += row.AllocatedAmount * row.EducationAccountAmount / row.PaymentAmount;
            onlinePaid += row.AllocatedAmount * row.OnlinePaymentAmount / row.PaymentAmount;
        }

        return new(
            enrollment,
            course,
            Money(paid),
            Money(educationPaid),
            Money(onlinePaid));
    }

    private static decimal Money(decimal amount)
        => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
