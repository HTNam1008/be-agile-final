using Microsoft.EntityFrameworkCore;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
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

    public async Task<bool> PersonHasActiveSchoolEnrollmentAsync(
        long personId,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken)
    {
        int exists = await dbContext.Database.SqlQuery<int>($"""
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1
                FROM [person].[SchoolEnrollment]
                WHERE [PersonId] = {personId}
                  AND [OrganizationId] = {organizationId}
                  AND [SchoolingStatusCode] = 'ACTIVE'
                  AND [StartDate] <= {onDate}
                  AND ([EndDate] IS NULL OR [EndDate] >= {onDate})
            ) THEN 1 ELSE 0 END AS int) AS [Value]
            """)
            .SingleAsync(cancellationToken);

        return exists == 1;
    }

    public Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken)
    {
        return dbContext.Set<CourseEnrollment>()
            .AnyAsync(
                x => x.PersonId == personId
                    && x.CourseId == courseId
                    && x.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled,
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

    public async Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillAsync(
        CourseEnrollment enrollment,
        string billNumber,
        DateTime issuedAtUtc,
        DateOnly dueDate,
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
                    billNumber,
                    issuedAtUtc,
                    dueDate,
                    feeLines,
                    cancellationToken);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            CourseEnrollmentBillingResult result = await AddEnrollmentAndBillRowsAsync(
                enrollment,
                billNumber,
                issuedAtUtc,
                dueDate,
                feeLines,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
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
        string billNumber,
        DateTime issuedAtUtc,
        DateOnly dueDate,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        decimal grossAmount = feeLines.Sum(x => x.FeeValue);
        Result<Bill> billResult = Bill.IssueForCourseEnrollment(
            enrollment.Id,
            billNumber,
            issuedAtUtc,
            dueDate,
            grossAmount);

        if (billResult.IsFailure)
        {
            throw new InvalidOperationException(billResult.Error.Message);
        }

        await dbContext.Set<Bill>().AddAsync(billResult.Value, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (CourseFeeBillingLine feeLine in feeLines)
        {
            Result<BillLine> billLineResult = BillLine.FromCourseFee(
                billResult.Value.Id,
                feeLine.FeeComponentId,
                feeLine.CourseFeeId,
                feeLine.FeeComponentName,
                feeLine.FeeValue);

            if (billLineResult.IsFailure)
            {
                throw new InvalidOperationException(billLineResult.Error.Message);
            }

            await dbContext.Set<BillLine>().AddAsync(billLineResult.Value, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new CourseEnrollmentBillingResult(enrollment, billResult.Value, feeLines.Count);
    }
}
