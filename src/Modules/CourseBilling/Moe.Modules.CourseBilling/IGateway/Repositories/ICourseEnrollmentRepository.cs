using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface ICourseEnrollmentRepository
{
    Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken);

    Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken);

    Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken);

    Task<bool> PersonHasActiveSchoolEnrollmentAsync(
        long personId,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CourseFeeBillingLine>> ListActiveCourseFeesAsync(
        long courseId,
        CancellationToken cancellationToken);

    Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken);

    Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillAsync(
        CourseEnrollment enrollment,
        string billNumber,
        DateTime issuedAtUtc,
        DateOnly dueDate,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken);
}

internal sealed record CourseFeeBillingLine(
    long CourseFeeId,
    long FeeComponentId,
    string FeeComponentName,
    decimal FeeValue);

internal sealed record CourseEnrollmentBillingResult(
    CourseEnrollment Enrollment,
    Bill Bill,
    int BillLineCount);
