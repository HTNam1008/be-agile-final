using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.SharedKernel.Results;
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
        long? enrollmentId = await dbContext.Set<Bill>()
            .Where(bill => bill.Id == billId)
            .Select(bill => (long?)bill.CourseEnrollmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (enrollmentId is null)
        {
            return;
        }

        await MarkEnrollmentsRefundedAsync([enrollmentId.Value], refundedAtUtc, cancellationToken);
    }

    public async Task ApplyFullRefundForBillsAsync(
        IReadOnlyCollection<long> billIds,
        DateTime refundedAtUtc,
        CancellationToken cancellationToken)
    {
        if (billIds.Count == 0)
        {
            return;
        }

        long[] enrollmentIds = await dbContext.Set<Bill>()
            .Where(bill => billIds.Contains(bill.Id))
            .Select(bill => bill.CourseEnrollmentId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        await MarkEnrollmentsRefundedAsync(enrollmentIds, refundedAtUtc, cancellationToken);
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
            join enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking() on bill.CourseEnrollmentId equals enrollment.Id
            join course in dbContext.Set<Course>().AsNoTracking() on enrollment.CourseId equals course.Id
            where item.BillingStatementId == statementId
                && bill.OutstandingAmount > 0m
                && bill.BillStatusCode != BillStatusCodes.Paid
                && bill.BillStatusCode != BillStatusCodes.Cancelled
            orderby bill.CurrentDueDate, bill.OriginalDueDate, bill.Id
            select new PayableStatementBill(
                item.Id,
                bill.Id,
                course.OrganizationId,
                bill.OutstandingAmount,
                bill.CurrentDueDate,
                bill.OriginalDueDate,
                dbContext.Set<Bill>().Count(candidate => candidate.CourseEnrollmentId == enrollment.Id) > 1,
                course.CourseCode,
                course.CourseName))
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
        var statementBillAmounts = await (
                from item in dbContext.Set<BillingStatementItem>()
                join bill in dbContext.Set<Bill>() on item.BillId equals bill.Id
                where item.BillingStatementId == statementId
                select new
                {
                    Item = item,
                    bill.NetPayableAmount,
                    bill.OutstandingAmount
                })
            .ToListAsync(cancellationToken);

        foreach (var row in statementBillAmounts)
        {
            row.Item.Refresh(
                row.NetPayableAmount,
                row.NetPayableAmount - row.OutstandingAmount);
        }

        decimal total = statementBillAmounts.Sum(x => x.NetPayableAmount);
        decimal outstanding = statementBillAmounts.Sum(x => x.OutstandingAmount);
        statement.Refresh(total, total - outstanding, paidAtUtc);
    }

    public async Task<Result> DeferStatementAsync(
        long statementId,
        long personId,
        IReadOnlyCollection<long> billIds,
        int maxDeferralCount,
        long actorLoginAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        BillingStatement statement = await dbContext.Set<BillingStatement>()
            .SingleAsync(x => x.Id == statementId && x.PersonId == personId, cancellationToken);
        long[] requestedBillIds = billIds.Distinct().ToArray();
        List<Bill> bills = await (
            from item in dbContext.Set<BillingStatementItem>()
            join bill in dbContext.Set<Bill>() on item.BillId equals bill.Id
            where item.BillingStatementId == statementId
                && requestedBillIds.Contains(bill.Id)
                && bill.OutstandingAmount > 0m
            select bill).ToListAsync(cancellationToken);
        foreach (Bill bill in bills)
        {
            DateOnly from = bill.CurrentDueDate;
            decimal amount = bill.OutstandingAmount;
            var result = bill.DeferToNextMonth(maxDeferralCount, utcNow);
            if (result.IsFailure)
                return result;

            await dbContext.Set<BillDeferral>().AddAsync(new BillDeferral(
                bill.Id,
                bill.CourseEnrollmentId,
                null,
                from,
                bill.CurrentDueDate,
                amount,
                bill.DeferralCount,
                actorLoginAccountId,
                utcNow), cancellationToken);
        }
        statement.Refresh(statement.TotalAmount, statement.PaidAmount, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<BillingPolicySnapshot> GetBillingPolicyAsync(
        long organizationId,
        CancellationToken cancellationToken)
    {
        OrganizationBillingConfiguration? configuration = await dbContext.Set<OrganizationBillingConfiguration>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);

        return configuration is null
            ? new BillingPolicySnapshot(
                organizationId,
                BillDeferralPolicy.DefaultMaxDeferralCount,
                BillDeferralPolicy.DefaultRejectionGracePeriodDays)
            : new BillingPolicySnapshot(
                organizationId,
                configuration.MaxDeferralCount,
                configuration.RejectionGracePeriodDays);
    }

    private async Task MarkEnrollmentsRefundedAsync(
        IReadOnlyCollection<long> enrollmentIds,
        DateTime refundedAtUtc,
        CancellationToken cancellationToken)
    {
        if (enrollmentIds.Count == 0)
        {
            return;
        }

        List<CourseEnrollment> enrollments = await dbContext.Set<CourseEnrollment>()
            .Where(candidate => enrollmentIds.Contains(candidate.Id))
            .ToListAsync(cancellationToken);

        foreach (CourseEnrollment enrollment in enrollments)
        {
            if (enrollment.EnrollmentStatusCode == CourseEnrollmentStatusCodes.Refunded)
            {
                continue;
            }

            enrollment.MarkRefunded(refundedAtUtc);
        }
    }
}
