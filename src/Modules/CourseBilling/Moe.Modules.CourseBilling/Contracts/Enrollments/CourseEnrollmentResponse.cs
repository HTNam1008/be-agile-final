namespace Moe.Modules.CourseBilling.Contracts.Enrollments;

public sealed record CourseEnrollmentResponse(
    long CourseEnrollmentId,
    long PersonId,
    long CourseId,
    string EnrollmentSourceCode,
    long EnrolledByLoginAccountId,
    string EnrollmentStatusCode,
    long? BillId,
    string? BillNumber,
    string? BillStatusCode,
    int BillLineCount,
    decimal GrossAmount,
    decimal NetPayableAmount,
    decimal OutstandingAmount);
