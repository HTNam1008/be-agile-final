using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class AdminCourseRepository(
    MoeDbContext dbContext,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationWriter notificationWriter,
    ILogger<AdminCourseRepository> logger) : IAdminCourseRepository
{
    public async Task<PageResponse<CourseSummaryDto>> ListCoursesAsync(
        CourseQueryRequest request,
        IReadOnlyCollection<long>? scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        IQueryable<Course> query = dbContext.Set<Course>().AsNoTracking();

        if (scopedOrganizationIds is not null)
        {
            query = query.Where(x => scopedOrganizationIds.Contains(x.OrganizationId));
        }

        if (request.OrganizationId.HasValue)
        {
            query = query.Where(x => x.OrganizationId == request.OrganizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            string keyword = request.Keyword.Trim();
            query = query.Where(x => x.CourseCode.Contains(keyword) || x.CourseName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.CourseName))
        {
            string courseName = request.CourseName.Trim();
            query = query.Where(x => x.CourseName.Contains(courseName));
        }

        if (!string.IsNullOrWhiteSpace(request.StatusCode))
        {
            string statusCode = request.StatusCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CourseStatusCode == statusCode);
        }

        if (request.StartDate.HasValue)
        {
            DateOnly filterStartDate = request.StartDate.Value;
            query = query.Where(x => x.EndDate >= filterStartDate);
        }

        if (request.EndDate.HasValue)
        {
            DateOnly filterEndDate = request.EndDate.Value;
            query = query.Where(x => x.StartDate <= filterEndDate);
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<Course> courses = await ApplyCourseSort(query, request)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        long[] courseIds = courses.Select(x => x.Id).ToArray();
        List<CourseFeeProjection> feeProjections = await (
                from fee in dbContext.Set<CourseFee>().AsNoTracking()
                join component in dbContext.Set<FeeComponent>().AsNoTracking()
                    on fee.FeeComponentId equals component.Id
                where courseIds.Contains(fee.CourseId) && fee.IsActive && component.IsActive
                select new CourseFeeProjection(
                    fee.CourseId,
                    fee.Id,
                    component.Id,
                    component.ComponentName,
                    fee.FeeValue,
                    component.CalculationTypeCode,
                    component.IsTaxComponent))
            .ToListAsync(cancellationToken);
        Dictionary<long, decimal> feeTotals = feeProjections
            .GroupBy(x => x.CourseId)
            .ToDictionary(
                group => group.Key,
                group => CourseFeeAmountCalculator.Calculate(group.Select(x => x.ToBillingLine()).ToArray()).Sum(x => x.Amount));

        Dictionary<long, int> enrollmentCounts = await dbContext.Set<CourseEnrollment>()
            .AsNoTracking()
            .Where(enrollment => courseIds.Contains(enrollment.CourseId)
                && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled)
            .GroupBy(enrollment => enrollment.CourseId)
            .Select(group => new { CourseId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.CourseId, x => x.Count, cancellationToken);

        List<CourseSummaryDto> items = courses
            .Select(x => new CourseSummaryDto(
                x.Id,
                x.OrganizationId,
                x.CourseCode,
                x.CourseName,
                x.Description,
                x.StartDate,
                x.EndDate,
                x.EnrollmentOpenAtUtc,
                x.EnrollmentCloseAtUtc,
                x.BeforeStartRefundPercentage,
                x.AfterStartRefundPercentage,
                x.CourseStatusCode,
                feeTotals.GetValueOrDefault(x.Id),
                enrollmentCounts.GetValueOrDefault(x.Id),
                x.UpdatedAtUtc,
                x.DisabledAtUtc))
            .ToList();

        return new PageResponse<CourseSummaryDto>(items, page, pageSize, total);
    }

    private IQueryable<Course> ApplyCourseSort(IQueryable<Course> query, CourseQueryRequest request)
    {
        bool descending = string.Equals(request.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        string sortBy = request.SortBy?.Trim() ?? string.Empty;

        return sortBy.ToLowerInvariant() switch
        {
            "code" => descending
                ? query.OrderByDescending(x => x.CourseCode).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CourseCode).ThenBy(x => x.Id),
            "name" => descending
                ? query.OrderByDescending(x => x.CourseName).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CourseName).ThenBy(x => x.Id),
            "period" => descending
                ? query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.EndDate).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.StartDate).ThenBy(x => x.EndDate).ThenBy(x => x.Id),
            "lastenrollment" => descending
                ? query.OrderByDescending(x => x.EnrollmentCloseAtUtc).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.EnrollmentCloseAtUtc).ThenBy(x => x.Id),
            "enrolled" => SortByEnrollmentCount(query, descending),
            "totalfee" => SortByTotalFee(query, descending),
            "status" => descending
                ? query.OrderByDescending(x => x.CourseStatusCode).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CourseStatusCode).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Id)
        };
    }

    private IQueryable<Course> SortByEnrollmentCount(IQueryable<Course> query, bool descending)
    {
        return descending
            ? query.OrderByDescending(course => dbContext.Set<CourseEnrollment>()
                    .Count(enrollment => enrollment.CourseId == course.Id
                        && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled))
                .ThenByDescending(course => course.Id)
            : query.OrderBy(course => dbContext.Set<CourseEnrollment>()
                    .Count(enrollment => enrollment.CourseId == course.Id
                        && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled))
                .ThenBy(course => course.Id);
    }

    private IQueryable<Course> SortByTotalFee(IQueryable<Course> query, bool descending)
    {
        IQueryable<CourseFeeTotalSortProjection> totalFeeQuery =
            from fee in dbContext.Set<CourseFee>().AsNoTracking()
            join component in dbContext.Set<FeeComponent>().AsNoTracking()
                on fee.FeeComponentId equals component.Id
            where fee.IsActive && component.IsActive
            group new { fee, component } by fee.CourseId
            into feeGroup
            select new CourseFeeTotalSortProjection(
                feeGroup.Key,
                feeGroup.Sum(x => !x.component.IsTaxComponent
                    && x.component.CalculationTypeCode != FeeComponentCalculationTypes.Percentage
                        ? x.fee.FeeValue
                        : 0m),
                feeGroup.Sum(x => x.component.IsTaxComponent
                    && x.component.CalculationTypeCode == FeeComponentCalculationTypes.Percentage
                        ? x.fee.FeeValue
                        : 0m),
                feeGroup.Sum(x => x.component.IsTaxComponent
                    && x.component.CalculationTypeCode != FeeComponentCalculationTypes.Percentage
                        ? x.fee.FeeValue
                        : 0m));

        var sortableQuery =
            from course in query
            join feeTotal in totalFeeQuery on course.Id equals feeTotal.CourseId into feeTotals
            from feeTotal in feeTotals.DefaultIfEmpty()
            select new
            {
                Course = course,
                TotalFee = feeTotal == null
                    ? 0m
                    : feeTotal.Subtotal + feeTotal.TaxFixedAmount + (feeTotal.Subtotal * feeTotal.TaxPercentage / 100m)
            };

        return descending
            ? sortableQuery.OrderByDescending(x => x.TotalFee).ThenByDescending(x => x.Course.Id).Select(x => x.Course)
            : sortableQuery.OrderBy(x => x.TotalFee).ThenBy(x => x.Course.Id).Select(x => x.Course);
    }

    public async Task<int> DisableEndedCoursesAsync(
        DateOnly today,
        DateTime utcNow,
        long actorLoginAccountId,
        IReadOnlyCollection<long>? scopedOrganizationIds,
        CancellationToken cancellationToken)
    {
        IQueryable<Course> query = dbContext.Set<Course>()
            .Where(x => x.EndDate < today && x.CourseStatusCode != CourseStatusCodes.Disabled);

        if (scopedOrganizationIds is not null)
        {
            query = query.Where(x => scopedOrganizationIds.Contains(x.OrganizationId));
        }

        List<Course> endedCourses = await query.ToListAsync(cancellationToken);

        foreach (Course course in endedCourses)
        {
            course.Disable(actorLoginAccountId, utcNow);
        }

        if (endedCourses.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return endedCourses.Count;
    }

    public Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken)
        => dbContext.Set<Course>().SingleOrDefaultAsync(x => x.Id == courseId, cancellationToken);

    public async Task<CourseAggregate?> GetCourseAggregateAsync(long courseId, CancellationToken cancellationToken)
    {
        Course? course = await dbContext.Set<Course>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == courseId, cancellationToken);

        if (course is null)
        {
            return null;
        }

        List<CourseMaterial> materials = await dbContext.Set<CourseMaterial>().AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.MaterialTitle)
            .ToListAsync(cancellationToken);

        List<CourseFeeDetail> fees = await ListFeesQuery(courseId, activeOnly: true)
            .ToListAsync(cancellationToken);

        var enrollmentCounts = await dbContext.Set<CourseEnrollment>().AsNoTracking()
            .Where(x => x.CourseId == courseId)
            .GroupBy(x => 1)
            .Select(g => new
            {
                Total = g.Count(),
                PendingPayment = g.Count(x => x.EnrollmentStatusCode == CourseEnrollmentStatusCodes.PendingPayment),
                Cancelled = g.Count(x => x.EnrollmentStatusCode == CourseEnrollmentStatusCodes.Cancelled)
            })
            .SingleOrDefaultAsync(cancellationToken);

        CourseEnrollmentSummaryDto summary = enrollmentCounts is null
            ? new CourseEnrollmentSummaryDto(0, 0, 0)
            : new CourseEnrollmentSummaryDto(enrollmentCounts.PendingPayment, enrollmentCounts.Cancelled, enrollmentCounts.Total);

        return new CourseAggregate(course, materials, fees, summary);
    }

    public Task<bool> CourseCodeExistsAsync(long organizationId, string courseCode, long? excludeCourseId, CancellationToken cancellationToken)
    {
        string normalizedCourseCode = courseCode.Trim();

        return dbContext.Set<Course>().AnyAsync(x =>
            x.OrganizationId == organizationId
            && x.CourseCode == normalizedCourseCode
            && (excludeCourseId == null || x.Id != excludeCourseId.Value), cancellationToken);
    }

    public async Task AddCourseAsync(Course course, CancellationToken cancellationToken)
    {
        await dbContext.Set<Course>().AddAsync(course, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveCourseAsync(Course course, CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task RemoveDraftCourseAsync(long courseId, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Set<CourseTarget>()
                .Where(x => x.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<CourseMaterial>()
                .Where(x => x.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<CourseFee>()
                .Where(x => x.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<CourseEnrollment>()
                .Where(x => x.CourseId == courseId)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Set<Course>()
                .Where(x => x.Id == courseId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    public async Task<IReadOnlyList<CourseMaterial>> ListMaterialsAsync(long courseId, CancellationToken cancellationToken)
        => await dbContext.Set<CourseMaterial>().AsNoTracking()
            .Where(x => x.CourseId == courseId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.MaterialTitle)
            .ToListAsync(cancellationToken);

    public Task<CourseMaterial?> FindMaterialAsync(long courseId, long courseMaterialId, CancellationToken cancellationToken)
        => dbContext.Set<CourseMaterial>()
            .SingleOrDefaultAsync(x => x.CourseId == courseId && x.Id == courseMaterialId, cancellationToken);

    public async Task AddMaterialAsync(CourseMaterial material, CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseMaterial>().AddAsync(material, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveMaterialAsync(CourseMaterial material, CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<CourseFeeDetail>> ListFeesAsync(long courseId, CancellationToken cancellationToken)
        => await ListFeesQuery(courseId).ToListAsync(cancellationToken);

    public Task<FeeComponent?> FindActiveFeeComponentAsync(long feeComponentId, CancellationToken cancellationToken)
        => dbContext.Set<FeeComponent>()
            .SingleOrDefaultAsync(x => x.Id == feeComponentId && x.IsActive, cancellationToken);

    public Task<FeeComponent?> FindActiveFeeComponentByCodeAsync(
        string componentCode,
        CancellationToken cancellationToken)
    {
        string normalizedCode = componentCode.Trim().ToUpperInvariant();
        return dbContext.Set<FeeComponent>()
            .SingleOrDefaultAsync(
                x => x.ComponentCode == normalizedCode && x.IsActive,
                cancellationToken);
    }

    public Task<CourseFee?> FindCourseFeeAsync(long courseId, long courseFeeId, CancellationToken cancellationToken)
        => dbContext.Set<CourseFee>()
            .SingleOrDefaultAsync(x => x.CourseId == courseId && x.Id == courseFeeId, cancellationToken);

    public Task<CourseFee?> FindCourseFeeByComponentAsync(long courseId, long feeComponentId, CancellationToken cancellationToken)
        => dbContext.Set<CourseFee>()
            .SingleOrDefaultAsync(x => x.CourseId == courseId && x.FeeComponentId == feeComponentId, cancellationToken);

    public async Task AddFeeAsync(CourseFee fee, CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseFee>().AddAsync(fee, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveFeeAsync(CourseFee fee, CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public Task<bool> HasActiveEnrollmentAsync(long courseId, long personId, CancellationToken cancellationToken)
        => dbContext.Set<CourseEnrollment>().AnyAsync(x =>
            x.CourseId == courseId
            && x.PersonId == personId
            && x.EnrollmentStatusCode == CourseEnrollmentStatusCodes.PendingPayment,
            cancellationToken);

    public async Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken)
    {
        await dbContext.Set<CourseEnrollment>().AddAsync(enrollment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminCourseEnrollmentDto>> ListEnrollmentsAsync(long courseId, CancellationToken cancellationToken)
        => await (
                from enrollment in dbContext.Set<CourseEnrollment>().AsNoTracking()
                join course in dbContext.Set<Course>().AsNoTracking()
                    on enrollment.CourseId equals course.Id
                join person in dbContext.Set<Person>().AsNoTracking()
                    on enrollment.PersonId equals person.Id into people
                from person in people.DefaultIfEmpty()
                where enrollment.CourseId == courseId
                let schoolEnrollment = dbContext.Set<SchoolEnrollment>().AsNoTracking()
                    .Where(x => x.PersonId == enrollment.PersonId
                        && x.OrganizationId == course.OrganizationId)
                    .OrderByDescending(x => x.StartDate)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefault()
                orderby enrollment.EnrolledAtUtc descending
                select new AdminCourseEnrollmentDto(
                    enrollment.Id,
                    enrollment.CourseId,
                    enrollment.PersonId,
                    person == null ? null : person.OfficialFullName,
                    schoolEnrollment == null ? null : schoolEnrollment.StudentNumber,
                    schoolEnrollment == null ? null : schoolEnrollment.LevelCode,
                    schoolEnrollment == null ? null : schoolEnrollment.ClassCode,
                    enrollment.EnrollmentSourceCode,
                    enrollment.EnrolledByLoginAccountId,
                    enrollment.EnrolledAtUtc,
                    enrollment.EnrollmentStatusCode))
            .ToListAsync(cancellationToken);

    public Task<CourseEnrollment?> FindEnrollmentAsync(long courseEnrollmentId, CancellationToken cancellationToken)
        => dbContext.Set<CourseEnrollment>().SingleOrDefaultAsync(x => x.Id == courseEnrollmentId, cancellationToken);

    public async Task<int> IssueBillsForUnbilledEnrollmentsAsync(
        long courseId,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly dueDate,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CourseFeeDetail> fees = await ListFeesQuery(courseId, activeOnly: true)
            .ToListAsync(cancellationToken);

        if (fees.Count == 0)
        {
            return 0;
        }

        List<CourseEnrollment> enrollments = await dbContext.Set<CourseEnrollment>()
            .Where(enrollment => enrollment.CourseId == courseId
                && enrollment.CoursePaymentPlanId != null
                && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.PendingPlanSelection
                && enrollment.EnrollmentStatusCode != CourseEnrollmentStatusCodes.Cancelled
                && !dbContext.Set<Bill>().Any(bill => bill.CourseEnrollmentId == enrollment.Id))
            .OrderBy(enrollment => enrollment.Id)
            .ToListAsync(cancellationToken);

        if (enrollments.Count == 0)
        {
            return 0;
        }

        Course course = await dbContext.Set<Course>().AsNoTracking()
            .SingleAsync(x => x.Id == courseId, cancellationToken);
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            List<(long PersonId, long BillId, string BillNumber, DateOnly DueDate, decimal NetPayableAmount)> issuedNotifications = [];
            int issuedCount = 0;
            IReadOnlyList<CourseFeeBillingAmount> feeAmounts = CourseFeeAmountCalculator.Calculate(fees);
            decimal grossAmount = feeAmounts.Sum(x => x.Amount);

            foreach (CourseEnrollment enrollment in enrollments)
            {
                Result<Bill> billResult = Bill.IssueForCourseEnrollment(
                    enrollment.Id,
                    $"{billNumberPrefix}-{enrollment.Id}".ToUpperInvariant(),
                    issuedAtUtc,
                    dueDate,
                    grossAmount);

                if (billResult.IsFailure)
                {
                    throw new InvalidOperationException(billResult.Error.Message);
                }

                await dbContext.Set<Bill>().AddAsync(billResult.Value, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);

                foreach (CourseFeeBillingAmount amount in feeAmounts.Where(x => x.Amount > 0m))
                {
                    Result<BillLine> billLineResult = BillLine.FromCourseFee(
                        billResult.Value.Id,
                        amount.FeeComponentId,
                        amount.CourseFeeId,
                        amount.FeeComponentName,
                        amount.Amount);

                    if (billLineResult.IsFailure)
                    {
                        throw new InvalidOperationException(billLineResult.Error.Message);
                    }

                    await dbContext.Set<BillLine>().AddAsync(billLineResult.Value, cancellationToken);
                }

                issuedNotifications.Add((
                    enrollment.PersonId,
                    billResult.Value.Id,
                    billResult.Value.BillNumber,
                    billResult.Value.CurrentDueDate,
                    billResult.Value.NetPayableAmount));
                issuedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (var notification in issuedNotifications)
            {
                await NotifyBillIssuedAsync(
                    notification.PersonId,
                    course,
                    notification.BillNumber,
                    notification.DueDate,
                    notification.NetPayableAmount,
                    cancellationToken);
            }

            return issuedCount;
        });
    }

    public async Task<Result> CancelEnrollmentAndBillAsync(
        CourseEnrollment enrollment,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        Bill? bill = await dbContext.Set<Bill>()
            .SingleOrDefaultAsync(x => x.CourseEnrollmentId == enrollment.Id, cancellationToken);

        if (bill is null)
        {
            enrollment.Cancel(utcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        Result billCancellation = bill.Cancel();
        if (billCancellation.IsFailure)
        {
            return billCancellation;
        }

        enrollment.Cancel(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<CourseFeeDetail> ListFeesQuery(long courseId, bool activeOnly = false)
        => from fee in dbContext.Set<CourseFee>().AsNoTracking()
           join component in dbContext.Set<FeeComponent>().AsNoTracking()
                on fee.FeeComponentId equals component.Id
           where fee.CourseId == courseId && (!activeOnly || fee.IsActive)
           orderby fee.SequenceNumber, component.ComponentName
           select new CourseFeeDetail(fee, component);

    private async Task NotifyBillIssuedAsync(
        long personId,
        Course course,
        string billNumber,
        DateOnly dueDate,
        decimal netPayableAmount,
        CancellationToken cancellationToken)
    {
        long? userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, cancellationToken);
        if (userAccountId is null)
        {
            logger.LogWarning("Bill issued notification skipped because no user account was found for person {PersonId}.", personId);
            return;
        }

        Result<long> result = await notificationWriter.CreateAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.BillIssued,
                $"Bill Issued: {billNumber}",
                $"New bill for {course.CourseName}. Net Payable: {netPayableAmount:N2}. Due: {dueDate:yyyy-MM-dd}."),
            cancellationToken);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Bill issued notification failed. PersonId={PersonId} BillNumber={BillNumber} Error={ErrorCode}",
                personId,
                billNumber,
                result.Error.Code);
        }
    }
}

internal sealed record CourseFeeProjection(
    long CourseId,
    long CourseFeeId,
    long FeeComponentId,
    string FeeComponentName,
    decimal FeeValue,
    string CalculationTypeCode,
    bool IsTaxComponent)
{
    public CourseFeeBillingLine ToBillingLine()
        => new(CourseFeeId, FeeComponentId, FeeComponentName, CalculationTypeCode, IsTaxComponent, FeeValue);
}

internal sealed record CourseFeeTotalSortProjection(
    long CourseId,
    decimal Subtotal,
    decimal TaxPercentage,
    decimal TaxFixedAmount);
