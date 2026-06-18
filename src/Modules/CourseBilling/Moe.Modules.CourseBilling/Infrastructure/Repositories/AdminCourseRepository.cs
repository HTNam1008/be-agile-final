using Microsoft.EntityFrameworkCore;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.CourseBilling.Infrastructure.Repositories;

internal sealed class AdminCourseRepository(MoeDbContext dbContext) : IAdminCourseRepository
{
    public async Task<PageResponse<CourseSummaryDto>> ListCoursesAsync(CourseQueryRequest request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        IQueryable<Course> query = dbContext.Set<Course>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            string keyword = request.Keyword.Trim();
            query = query.Where(x => x.CourseCode.Contains(keyword) || x.CourseName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.StatusCode))
        {
            string statusCode = request.StatusCode.Trim();
            query = query.Where(x => x.CourseStatusCode == statusCode);
        }

        long total = await query.LongCountAsync(cancellationToken);
        List<CourseSummaryDto> items = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CourseSummaryDto(
                x.Id,
                x.CourseCode,
                x.CourseName,
                x.Description,
                x.StartDate,
                x.EndDate,
                x.EnrollmentOpenAtUtc,
                x.EnrollmentCloseAtUtc,
                x.CourseStatusCode,
                x.UpdatedAtUtc,
                x.DisabledAtUtc))
            .ToListAsync(cancellationToken);

        return new PageResponse<CourseSummaryDto>(items, page, pageSize, total);
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

    public Task<bool> CourseCodeExistsAsync(string courseCode, long? excludeCourseId, CancellationToken cancellationToken)
    {
        string normalizedCourseCode = courseCode.Trim();

        return dbContext.Set<Course>().AnyAsync(x =>
            x.CourseCode == normalizedCourseCode
            && (excludeCourseId == null || x.Id != excludeCourseId.Value), cancellationToken);
    }

    public async Task AddCourseAsync(Course course, CancellationToken cancellationToken)
    {
        await dbContext.Set<Course>().AddAsync(course, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

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

    public async Task<IReadOnlyList<CourseFeeDetail>> ListFeesAsync(long courseId, CancellationToken cancellationToken)
        => await ListFeesQuery(courseId).ToListAsync(cancellationToken);

    public Task<FeeComponent?> FindActiveFeeComponentAsync(long feeComponentId, CancellationToken cancellationToken)
        => dbContext.Set<FeeComponent>()
            .SingleOrDefaultAsync(x => x.Id == feeComponentId && x.IsActive, cancellationToken);

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
        => await dbContext.Set<CourseEnrollment>().AsNoTracking()
            .Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.EnrolledAtUtc)
            .Select(x => new AdminCourseEnrollmentDto(
                x.Id,
                x.CourseId,
                x.PersonId,
                null,
                x.EnrollmentSourceCode,
                x.EnrolledByLoginAccountId,
                x.EnrolledAtUtc,
                x.EnrollmentStatusCode))
            .ToListAsync(cancellationToken);

    public Task<CourseEnrollment?> FindEnrollmentAsync(long courseEnrollmentId, CancellationToken cancellationToken)
        => dbContext.Set<CourseEnrollment>().SingleOrDefaultAsync(x => x.Id == courseEnrollmentId, cancellationToken);

    public void RemoveEnrollment(CourseEnrollment enrollment)
        => dbContext.Set<CourseEnrollment>().Remove(enrollment);

    private IQueryable<CourseFeeDetail> ListFeesQuery(long courseId, bool activeOnly = false)
        => from fee in dbContext.Set<CourseFee>().AsNoTracking()
           join component in dbContext.Set<FeeComponent>().AsNoTracking()
                on fee.FeeComponentId equals component.Id
           where fee.CourseId == courseId && (!activeOnly || fee.IsActive)
           orderby fee.SequenceNumber, component.ComponentName
           select new CourseFeeDetail(fee, component);
}
