namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

public sealed record BulkImportStudentWorkbookRow(
    int RowNumber,
    string? SchoolName,
    long? OrganizationId,
    string IdentityNumber,
    string FullName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string? CitizenshipStatusCode,
    string StudentNumber,
    string AcademicYear,
    string LevelCode,
    string ClassCode,
    DateOnly? StartDate,
    string? Email,
    string? ContactNumber,
    string? Address,
    bool IsTemplateSampleRow = false);
