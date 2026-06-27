namespace Moe.Modules.IdentityPlatform.Application.Students.BulkImportStudents;

public static class BulkImportStudentWorkbookColumns
{
    public const string SchoolName = nameof(SchoolName);
    public const string OrganizationId = nameof(OrganizationId);
    public const string IdentityNumber = nameof(IdentityNumber);
    public const string FullName = nameof(FullName);
    public const string DateOfBirth = nameof(DateOfBirth);
    public const string NationalityCode = nameof(NationalityCode);
    public const string CitizenshipStatusCode = nameof(CitizenshipStatusCode);
    public const string StudentNumber = nameof(StudentNumber);
    public const string AcademicYear = nameof(AcademicYear);
    public const string LevelCode = nameof(LevelCode);
    public const string ClassCode = nameof(ClassCode);
    public const string StartDate = nameof(StartDate);
    public const string Email = nameof(Email);
    public const string Mobile = nameof(Mobile);
    public const string Address = nameof(Address);

    public static IReadOnlyList<string> Headers { get; } =
    [
        SchoolName,
        OrganizationId,
        IdentityNumber,
        FullName,
        DateOfBirth,
        NationalityCode,
        CitizenshipStatusCode,
        StudentNumber,
        AcademicYear,
        LevelCode,
        ClassCode,
        StartDate,
        Email,
        Mobile,
        Address
    ];

    public static IReadOnlySet<string> NullableHeaders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SchoolName,
        OrganizationId,
        CitizenshipStatusCode,
        ClassCode,
        StartDate,
        Email,
        Mobile,
        Address
    };
}
