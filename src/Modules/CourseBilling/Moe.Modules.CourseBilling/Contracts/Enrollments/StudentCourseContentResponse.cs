namespace Moe.Modules.CourseBilling.Contracts.Enrollments;

public sealed record StudentCourseContentResponse(
    long CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyCollection<StudentCourseMaterialResponse> Materials);

public sealed record StudentCourseMaterialResponse(
    long CourseMaterialId,
    string MaterialTitle,
    string? MaterialDescription,
    string MaterialTypeCode,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    int DisplayOrder,
    bool IsRequired);

public sealed record StudentCourseMaterialOfficePreviewResponse(
    string PreviewUrl,
    DateTimeOffset ExpiresAtUtc);
