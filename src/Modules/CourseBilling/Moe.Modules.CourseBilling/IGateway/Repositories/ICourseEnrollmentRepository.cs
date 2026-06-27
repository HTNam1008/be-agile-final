using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;

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
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
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
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
        CancellationToken cancellationToken);

    CourseEnrollmentBillingPreviewResult PreviewPaymentPlanBills(
        CourseBillingPlan plan,
        bool installment,
        DateOnly firstDueDate,
        IReadOnlyCollection<CourseFeeBillingLine> feeLines,
        IReadOnlyCollection<CourseFasSubsidy> fasSubsidies);
}

internal sealed record CourseFeeBillingLine(
    long CourseFeeId,
    long FeeComponentId,
    string FeeComponentName,
    string CalculationTypeCode,
    bool IsTaxComponent,
    decimal FeeValue,
    string FeeComponentCode = "",
    string FeeComponentTypeCode = "");

internal sealed record CourseEnrollmentBillingResult(
    CourseEnrollment Enrollment,
    IReadOnlyCollection<GeneratedBillResult> Bills);

internal sealed record GeneratedBillResult(
    Bill Bill,
    int BillLineCount);

internal sealed record CourseEnrollmentBillingPreviewResult(
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetPayableAmount,
    IReadOnlyCollection<PreviewGeneratedBillResult> Bills);

internal sealed record PreviewGeneratedBillResult(
    int SequenceNumber,
    DateOnly CurrentDueDate,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetPayableAmount,
    bool IsInstallment,
    IReadOnlyCollection<PreviewGeneratedBillLineResult> Lines);

internal sealed record PreviewGeneratedBillLineResult(
    long FeeComponentId,
    long? CourseFeeId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    string Description,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetAmount);
