using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.FasPayment.IGateway.Payments;

internal sealed record EnrollmentCancellationSnapshot(
    CourseEnrollment Enrollment,
    Course Course,
    decimal PaidAmount,
    decimal EducationAccountPaidAmount,
    decimal OnlinePaidAmount,
    decimal OutstandingAmount,
    int OutstandingBillCount,
    IReadOnlyCollection<EnrollmentPaymentRefundSource> RefundSources);

internal sealed record EnrollmentPaymentRefundSource(
    long PaymentId,
    long? EducationAccountPaymentPartId,
    long? EducationAccountTransactionId,
    string? ProviderChargeId,
    decimal AllocatedAmount,
    decimal EducationAccountAllocatedAmount,
    decimal OnlineAllocatedAmount);

internal interface IEnrollmentRefundPreviewRepository
{
    Task<EnrollmentCancellationSnapshot?> FindAsync(
        long enrollmentId,
        long personId,
        CancellationToken cancellationToken);
}
