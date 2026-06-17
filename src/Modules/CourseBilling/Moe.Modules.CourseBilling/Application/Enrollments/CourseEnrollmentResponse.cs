namespace Moe.Modules.CourseBilling.Application.Enrollments;

public sealed record CourseEnrollmentResponse(
    long CourseEnrollmentId,
    long PersonId,
    long CourseId,
    string EnrollmentSourceCode,
    long EnrolledByLoginAccountId,
    string EnrollmentStatusCode,
    long BillId,
    string BillNumber,
    string BillStatusCode,
    decimal GrossAmount,
    decimal NetPayableAmount,
    decimal OutstandingAmount);
