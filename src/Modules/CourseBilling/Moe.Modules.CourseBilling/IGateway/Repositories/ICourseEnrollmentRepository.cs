using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.IGateway.Repositories;

internal interface ICourseEnrollmentRepository
{
    Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken);

    Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken);

    Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken);

    Task<long?> FindActiveStudentPersonIdAsync(
        string studentNumber,
        long organizationId,
        DateOnly onDate,
        CancellationToken cancellationToken);

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

    Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillsAsync(
        CourseEnrollment enrollment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        CancellationToken cancellationToken);

    Task<CourseEnrollment?> FindEnrollmentAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken);

    Task<CourseEnrollmentBillingResult?> ChangePaymentPlanAndReissueBillsAsync(
        CourseEnrollment enrollment,
        long coursePaymentPlanId,
        bool installment,
        string billNumberPrefix,
        DateTime issuedAtUtc,
        DateOnly firstDueDate,
        int installmentCount,
        int intervalMonths,
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
    IReadOnlyCollection<GeneratedBillResult> Bills);

internal sealed record GeneratedBillResult(
    Bill Bill,
    int BillLineCount);
