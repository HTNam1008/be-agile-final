using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal sealed record CourseAggregate(
    Course Course,
    IReadOnlyList<CourseMaterial> Materials,
    IReadOnlyList<CourseFeeDetail> Fees,
    CourseEnrollmentSummaryDto EnrollmentSummary);

internal sealed record CourseFeeDetail(CourseFee CourseFee, FeeComponent FeeComponent);

internal interface IAdminCourseRepository
{
    Task<PageResponse<CourseSummaryDto>> ListCoursesAsync(CourseQueryRequest request, CancellationToken cancellationToken);
    Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken);
    Task<CourseAggregate?> GetCourseAggregateAsync(long courseId, CancellationToken cancellationToken);
    Task<bool> CourseCodeExistsAsync(string courseCode, long? excludeCourseId, CancellationToken cancellationToken);
    Task AddCourseAsync(Course course, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseMaterial>> ListMaterialsAsync(long courseId, CancellationToken cancellationToken);
    Task<CourseMaterial?> FindMaterialAsync(long courseId, long courseMaterialId, CancellationToken cancellationToken);
    Task AddMaterialAsync(CourseMaterial material, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseFeeDetail>> ListFeesAsync(long courseId, CancellationToken cancellationToken);
    Task<FeeComponent?> FindActiveFeeComponentAsync(long feeComponentId, CancellationToken cancellationToken);
    Task<CourseFee?> FindCourseFeeAsync(long courseId, long courseFeeId, CancellationToken cancellationToken);
    Task<CourseFee?> FindCourseFeeByComponentAsync(long courseId, long feeComponentId, CancellationToken cancellationToken);
    Task AddFeeAsync(CourseFee fee, CancellationToken cancellationToken);
    Task<bool> HasActiveEnrollmentAsync(long courseId, long personId, CancellationToken cancellationToken);
    Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCourseEnrollmentDto>> ListEnrollmentsAsync(long courseId, CancellationToken cancellationToken);
    Task<CourseEnrollment?> FindEnrollmentAsync(long courseEnrollmentId, CancellationToken cancellationToken);
    void RemoveEnrollment(CourseEnrollment enrollment);
}
