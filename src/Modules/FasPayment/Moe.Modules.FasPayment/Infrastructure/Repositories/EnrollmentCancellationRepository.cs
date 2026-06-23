using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.FasPayment.Application;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class EnrollmentCancellationRepository(MoeDbContext dbContext)
    : IEnrollmentCancellationRepository
{
    public async Task<Result<string>> CancelEnrollmentAndOutstandingBillsAsync(
        long enrollmentId,
        long personId,
        bool refunded,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        CourseEnrollment? enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleOrDefaultAsync(
                x => x.Id == enrollmentId && x.PersonId == personId,
                cancellationToken);
        if (enrollment is null)
            return Result<string>.Failure(PaymentApplicationErrors.EnrollmentNotFound);

        if (enrollment.EnrollmentStatusCode is CourseEnrollmentStatusCodes.Cancelled or CourseEnrollmentStatusCodes.Refunded)
            return Result<string>.Success(enrollment.EnrollmentStatusCode);

        List<Bill> bills = await dbContext.Set<Bill>()
            .Where(x => x.CourseEnrollmentId == enrollment.Id)
            .ToListAsync(cancellationToken);

        foreach (Bill bill in bills)
        {
            if (bill.BillStatusCode is BillStatusCodes.Paid or BillStatusCodes.Cancelled)
                continue;

            Result billCancellation = bill.Cancel();
            if (billCancellation.IsFailure)
                return Result<string>.Failure(billCancellation.Error);
        }

        if (refunded)
            enrollment.MarkRefunded(utcNow);
        else
            enrollment.Cancel(utcNow);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<string>.Success(enrollment.EnrollmentStatusCode);
    }
}

