namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record EnrollmentCancellationResponse(
    long CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    bool Cancelled,
    string EnrollmentStatusCode,
    long? EnrollmentRefundId,
    string? RefundStatusCode,
    decimal PaidAmount,
    decimal RefundAmount,
    decimal EducationAccountRefundAmount,
    decimal OnlineRefundAmount,
    DateTime CancelledAtUtc);

