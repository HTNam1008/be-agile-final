using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.FasPayment.IGateway.Payments;

internal sealed record EnrollmentCancellationSnapshot(
    CourseEnrollment Enrollment,
    Course Course,
    decimal PaidAmount,
    decimal EducationAccountPaidAmount,
    decimal OnlinePaidAmount);

internal interface IEnrollmentRefundPreviewRepository
{
    Task<EnrollmentCancellationSnapshot?> FindAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken);
}
