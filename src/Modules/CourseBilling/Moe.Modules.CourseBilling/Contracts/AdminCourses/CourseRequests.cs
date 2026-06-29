namespace Moe.Modules.CourseBilling.Contracts.AdminCourses;

public sealed record CourseQueryRequest(
    long? OrganizationId = null,
    string? Keyword = null,
    string? StatusCode = null,
    string? CourseName = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int Page = 1,
    int PageSize = 10);

public sealed record CreateCourseRequest(
    long OrganizationId,
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    decimal BeforeStartRefundPercentage = 100m,
    decimal AfterStartRefundPercentage = 50m);

public sealed record UpdateCourseRequest(
    string CourseCode,
    string CourseName,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime EnrollmentOpenAt,
    DateTime EnrollmentCloseAt,
    decimal BeforeStartRefundPercentage = 100m,
    decimal AfterStartRefundPercentage = 50m);

public sealed record DisableCourseRequest;
