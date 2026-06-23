using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class CourseEnrollmentRepository(MoeDbContext dbContext) : ICourseEnrollmentRepository
{
    public async Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Course>()
            .AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => (long?)x.OrganizationId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Course>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, cancellationToken);
    }

    public async Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken)
    {
        int exists = await dbContext.Database.SqlQuery<int>($"""
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM [person].[Person]
                WHERE [PersonId] = {personId}
            ) THEN 1 ELSE 0 END AS int) AS [Value]
            """)
            .SingleAsync(cancellationToken);

        return exists == 1;
    }

    public Task<long?> FindActiveStudentPersonIdAsync(
        string studentNumber,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        string normalizedStudentNumber = studentNumber.Trim().ToUpperInvariant();

        return dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => x.StudentNumber == normalizedStudentNumber
                && x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= onDate
                && (x.EndDate == null || x.EndDate >= onDate))
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .Select(x => (long?)x.PersonId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> PersonHasActiveSchoolEnrollmentAsync(
        long personId,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken)
        => await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .AnyAsync(x => x.PersonId == personId
                && x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= onDate
                && (x.EndDate == null || x.EndDate >= onDate),
                cancellationToken);

    public Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken)
    {
        return dbContext.Set<CourseEnrollment>()
            .AnyAsync(
                x => x.PersonId == personId
                    && x.CourseId == courseId
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Refunded
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Exited,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<CourseFeeBillingLine>> ListActiveCourseFeesAsync(
        long courseId,
        CancellationToken cancellationToken)
    {
        return await (
                from courseFee in dbContext.Set<CourseFee>().AsNoTracking()
                join feeComponent in dbContext.Set<FeeComponent>().AsNoTracking()
                    on courseFee.FeeComponentId equals feeComponent.Id
                where courseFee.CourseId == courseId
                    && courseFee.IsActive
                    && feeComponent.IsActive
                orderby courseFee.SequenceNumber, courseFee.Id
                select new CourseFeeBillingLine(
                    courseFee.Id,
                    feeComponent.Id,
                    feeComponent.ComponentName,
                    courseFee.FeeValue))
            .ToArrayAsync(cancellationToken);
    }

    public async Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillsAsync(
        CourseEnrollment enrollment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            if (IsInMemoryDatabase())
            {
                return await AddEnrollmentAndBillRowsAsync(
                    enrollment,
                    billNumberPrefix,
                    issuedAtUtc,
                    firstDueDate,
                    installmentCount,
                    intervalMonths,
                    feeLines,
                    cancellationToken);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            CourseEnrollmentBillingResult result = await AddEnrollmentAndBillRowsAsync(
                enrollment,
                billNumberPrefix,
                issuedAtUtc,
                firstDueDate,
                installmentCount,
                intervalMonths,
                feeLines,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public Task<CourseEnrollment?> FindEnrollmentAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken)
        => dbContext.Set<CourseEnrollment>().SingleOrDefaultAsync(
            x => x.Id == enrollmentId && x.PersonId == personId,
            cancellationToken);

    public async Task<CourseEnrollmentBillingResult?> ChangePaymentPlanAndReissueBillsAsync(
        CourseEnrollment enrollment,
        long coursePaymentPlanId,
        bool installment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = IsInMemoryDatabase()
                ? null
                : await dbContext.Database.BeginTransactionAsync(cancellationToken);

            Bill[] existingBills = await dbContext.Set<Bill>()
                .Where(x => x.CourseEnrollmentId == enrollment.Id)
                .ToArrayAsync(cancellationToken);
            if (existingBills.Any(x => x.PaidAmount > 0m || x.BillStatusCode == BillStatusCodes.Paid))
                return null;
            int activePaymentAttempts = IsInMemoryDatabase()
                ? 0
                : await dbContext.Database.SqlQuery<int>($"""
                    SELECT COUNT(*) AS [Value]
                    FROM [payment].[PaymentAllocation] allocation
                    INNER JOIN [payment].[Payment] payment
                        ON payment.[PaymentId] = allocation.[PaymentId]
                    INNER JOIN [billing].[Bill] bill
                        ON bill.[BillId] = allocation.[BillId]
                    WHERE bill.[CourseEnrollmentId] = {enrollment.Id}
                      AND payment.[PaymentStatusCode] NOT IN ('FAILED', 'CANCELLED', 'EXPIRED')
                    """).SingleAsync(cancellationToken);
            if (activePaymentAttempts > 0)
                return null;

            foreach (Bill bill in existingBills)
                bill.Cancel();

            enrollment.ChangePaymentPlan(coursePaymentPlanId, installment);
            await dbContext.SaveChangesAsync(cancellationToken);

            long totalMinor = decimal.ToInt64(
                decimal.Round(feeLines.Sum(x => x.FeeValue), 2, MidpointRounding.AwayFromZero) * 100m);
            long baseMinor = totalMinor / installmentCount;
            long remainderMinor = totalMinor % installmentCount;
            List<GeneratedBillResult> generated = [];
            for (int sequence = 1; sequence <= installmentCount; sequence++)
            {
                decimal installmentAmount = (baseMinor + (sequence <= remainderMinor ? 1 : 0)) / 100m;
                DateOnly dueDate = firstDueDate.AddMonths((sequence - 1) * intervalMonths);
                Bill bill = Bill.IssueForCourseEnrollment(
                    enrollment.Id, $"{billNumberPrefix}-{sequence:D2}", issuedAtUtc,
                    dueDate, installmentAmount, sequenceNumber: sequence).Value;
                await dbContext.Set<Bill>().AddAsync(bill, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                BillLine line = BillLine.FromCourseFee(
                    bill.Id, feeLines.First().FeeComponentId, feeLines.First().CourseFeeId,
                    $"Course installment {sequence} of {installmentCount}", installmentAmount).Value;
                await dbContext.Set<BillLine>().AddAsync(line, cancellationToken);
                generated.Add(new GeneratedBillResult(bill, 1));
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new CourseEnrollmentBillingResult(enrollment, generated);
        });
    }

    private bool IsInMemoryDatabase()
    {
        return string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
    }

    private async Task<CourseEnrollmentBillingResult> AddEnrollmentAndBillRowsAsync(
        CourseEnrollment enrollment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (installmentCount <= 0 || intervalMonths < 0)
            throw new InvalidOperationException("The payment plan schedule is invalid.");

        long totalMinor = decimal.ToInt64(
            decimal.Round(feeLines.Sum(x => x.FeeValue), 2, MidpointRounding.AwayFromZero) * 100m);
        long baseMinor = totalMinor / installmentCount;
        long remainderMinor = totalMinor % installmentCount;
        List<GeneratedBillResult> generated = [];

        for (int sequence = 1; sequence <= installmentCount; sequence++)
        {
            decimal installmentAmount =
                (baseMinor + (sequence <= remainderMinor ? 1 : 0)) / 100m;
            DateOnly dueDate = firstDueDate.AddMonths((sequence - 1) * intervalMonths);
            Result<Bill> billResult = Bill.IssueForCourseEnrollment(
                enrollment.Id,
                $"{billNumberPrefix}-{sequence:D2}",
                issuedAtUtc,
                dueDate,
                installmentAmount,
                sequenceNumber: sequence);
            if (billResult.IsFailure)
                throw new InvalidOperationException(billResult.Error.Message);

            await dbContext.Set<Bill>().AddAsync(billResult.Value, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            Result<BillLine> lineResult = BillLine.FromCourseFee(
                billResult.Value.Id,
                feeLines.First().FeeComponentId,
                feeLines.First().CourseFeeId,
                $"Course installment {sequence} of {installmentCount}",
                installmentAmount);
            if (lineResult.IsFailure)
                throw new InvalidOperationException(lineResult.Error.Message);

            await dbContext.Set<BillLine>().AddAsync(lineResult.Value, cancellationToken);
            generated.Add(new GeneratedBillResult(billResult.Value, 1));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CourseEnrollmentBillingResult(enrollment, generated);
    }
}
