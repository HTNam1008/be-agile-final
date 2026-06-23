using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Payments;

internal sealed class CoursePaymentGateway(MoeDbContext dbContext) : ICoursePaymentGateway
{
    public Task<PayableCourseBill?> FindPayableBillAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken)
    {
        return (
            from bill in dbContext.Set<Bill>().AsNoTracking()
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking()
                on enrollment.CourseId equals course.Id
            where bill.Id == billId
                && enrollment.PersonId == personId
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            select new PayableCourseBill(
                bill.Id,
                enrollment.Id,
                course.Id,
                enrollment.PersonId,
                course.OrganizationId,
                course.CourseCode,
                course.CourseName,
                bill.OutstandingAmount,
                bill.BillStatusCode))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
        => dbContext.Set<Course>()
            .AsNoTracking()
            .Where(course => course.Id == courseId)
            .Select(course => (long?)course.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task ApplySuccessfulPaymentAsync(
        long billId,
        decimal amount,
        bool paidInFull,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        Bill bill = await dbContext.Set<Bill>()
            .SingleAsync(candidate => candidate.Id == billId, cancellationToken);
        CourseEnrollment enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleAsync(candidate => candidate.Id == bill.CourseEnrollmentId, cancellationToken);

        var result = bill.RecordPayment(amount, paidAtUtc);
        if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        Bill[] enrollmentBills = await dbContext.Set<Bill>()
            .Where(candidate => candidate.CourseEnrollmentId == enrollment.Id)
            .ToArrayAsync(cancellationToken);
        bool allBillsPaid = enrollmentBills.All(candidate =>
            candidate.BillStatusCode == BillStatusCodes.Paid ||
            candidate.BillStatusCode == BillStatusCodes.Cancelled);
        enrollment.GrantPaidAccess(allBillsPaid);
    }

    public async Task ApplyPaymentFailureAsync(long billId, CancellationToken cancellationToken)
    {
        long enrollmentId = await dbContext.Set<Bill>()
            .Where(bill => bill.Id == billId)
            .Select(bill => bill.CourseEnrollmentId)
            .SingleAsync(cancellationToken);
        CourseEnrollment enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleAsync(candidate => candidate.Id == enrollmentId, cancellationToken);
        enrollment.LockForPaymentFailure();
    }

    public async Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken)
    {
        long enrollmentId = await dbContext.Set<Bill>()
            .Where(bill => bill.Id == billId)
            .Select(bill => bill.CourseEnrollmentId)
            .SingleAsync(cancellationToken);
        CourseEnrollment enrollment = await dbContext.Set<CourseEnrollment>()
            .SingleAsync(candidate => candidate.Id == enrollmentId, cancellationToken);
        enrollment.MarkRefunded(refundedAtUtc);
    }

    public async Task<PayableStatement?> FindPayableStatementAsync(
        long statementId,
        long personId,
        CancellationToken cancellationToken)
    {
        BillingStatement? statement = await dbContext.Set<BillingStatement>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == statementId && x.PersonId == personId, cancellationToken);
        if (statement is null || statement.OutstandingAmount <= 0m) return null;

        PayableStatementBill[] bills = await (
            from item in dbContext.Set<BillingStatementItem>().AsNoTracking()
            join bill in dbContext.Set<Bill>().AsNoTracking() on item.BillId equals bill.Id
            where item.BillingStatementId == statementId
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Paid
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            orderby bill.CurrentDueDate, bill.OriginalDueDate, bill.Id
            select new PayableStatementBill(
                item.Id,
                bill.Id,
                bill.OutstandingAmount,
                bill.CurrentDueDate,
                bill.OriginalDueDate))
            .ToArrayAsync(cancellationToken);
        decimal total = bills.Sum(x => x.OutstandingAmount);
        return total <= 0m ? null : new(statement.Id, personId, total, statement.CurrencyCode, bills);
    }

    public async Task ApplyStatementPaymentAsync(
        long statementId,
        IReadOnlyCollection<BillPaymentAllocation> allocations,
        DateTime paidAtUtc,
        CancellationToken cancellationToken)
    {
        BillingStatement statement = await dbContext.Set<BillingStatement>()
            .SingleAsync(x => x.Id == statementId, cancellationToken);
        long[] billIds = allocations.Select(x => x.BillId).ToArray();
        List<Bill> bills = await dbContext.Set<Bill>()
            .Where(x => billIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (BillPaymentAllocation allocation in allocations)
        {
            Bill bill = bills.Single(x => x.Id == allocation.BillId);
            var result = bill.RecordPayment(allocation.Amount, paidAtUtc);
            if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
        }
        long[] enrollmentIds = bills.Select(x => x.CourseEnrollmentId).Distinct().ToArray();
        List<CourseEnrollment> enrollments = await dbContext.Set<CourseEnrollment>()
            .Where(x => enrollmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        List<Bill> enrollmentBills = await dbContext.Set<Bill>()
            .Where(x => enrollmentIds.Contains(x.CourseEnrollmentId))
            .ToListAsync(cancellationToken);
        foreach (CourseEnrollment enrollment in enrollments)
        {
            bool allBillsPaid = enrollmentBills
                .Where(x => x.CourseEnrollmentId == enrollment.Id)
                .All(x =>
                    x.BillStatusCode == BillStatusCodes.Paid ||
                    x.BillStatusCode == BillStatusCodes.Cancelled);
            enrollment.GrantPaidAccess(allBillsPaid);
        }
        decimal total = await dbContext.Set<BillingStatementItem>()
            .Where(x => x.BillingStatementId == statementId)
            .Join(dbContext.Set<Bill>(), item => item.BillId, bill => bill.Id, (_, bill) => bill.NetPayableAmount)
            .SumAsync(cancellationToken);
        decimal outstanding = bills.Sum(x => x.OutstandingAmount);
        statement.Refresh(total, total - outstanding, paidAtUtc);
    }

    public async Task DeferStatementAsync(
        long statementId,
        long personId,
        long failedPaymentId,
        long actorLoginAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        BillingStatement statement = await dbContext.Set<BillingStatement>()
            .SingleAsync(x => x.Id == statementId && x.PersonId == personId, cancellationToken);
        List<Bill> bills = await (
            from item in dbContext.Set<BillingStatementItem>()
            join bill in dbContext.Set<Bill>() on item.BillId equals bill.Id
            where item.BillingStatementId == statementId && bill.OutstandingAmount > 0m
            select bill).ToListAsync(cancellationToken);
        foreach (Bill bill in bills)
        {
            DateOnly from = bill.CurrentDueDate;
            decimal amount = bill.OutstandingAmount;
            var result = bill.DeferToNextMonth(failedPaymentId, utcNow);
            if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
            await dbContext.Set<BillDeferral>().AddAsync(new BillDeferral(
                bill.Id,
                bill.CourseEnrollmentId,
                failedPaymentId,
                from,
                bill.CurrentDueDate,
                amount,
                bill.DeferralCount,
                actorLoginAccountId,
                utcNow), cancellationToken);
        }
        statement.Refresh(statement.TotalAmount, statement.PaidAmount, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

}
