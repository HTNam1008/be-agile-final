namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal static class DemoSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const long HqAdminLoginAccountId = 1001;
    public const long SchoolAdminLoginAccountId = 1002;
    public const long StudentLoginAccountId = 1003;
    public const long StudentPersonId = 2001;
    public const long StudentSchoolEnrollmentId = 3001;
    public const long StudentEducationAccountId = 4001;

    public const string TenantId = "ea71ddeb-596c-4034-84d4-d65f91edc14a";
    public const string EntraIssuer = "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/";
    public const string HqAdminObjectId = "731f2a50-4fa7-4530-9294-1a5b912daf31";
    public const string SchoolAdminObjectId = "00000000-0000-0000-0000-000000000222";

    public const string MockPassIssuer = "http://localhost:5156/singpass/v3/fapi";
    public const string MockPassSubject = "ef39a074-b64d-4990-a937-6f80772e2bb8";
    public const string MockPassNric = "S1234567A";

    public static readonly IReadOnlyList<MockPassStudentSeed> MockPassStudents =
    [
        new(
            LoginAccountId: StudentLoginAccountId,
            PersonId: StudentPersonId,
            EnrollmentId: StudentSchoolEnrollmentId,
            EducationAccountId: StudentEducationAccountId,
            UserAccessScopeId: 1003,
            SingpassSubjectId: MockPassSubject,
            Nric: MockPassNric,
            FullName: "Tan Mei Ling",
            DateOfBirth: new DateOnly(2008, 5, 12),
            StudentNumber: "DEMO-STU-0001",
            LevelCode: "SEC_4",
            ClassCode: "4A",
            Email: "tan.mei.ling@student.example.test",
            Mobile: "+6590000001",
            Address: "1 Demo Street, Singapore 000001"),
        new(
            LoginAccountId: 1004,
            PersonId: 2002,
            EnrollmentId: 3002,
            EducationAccountId: 4002,
            UserAccessScopeId: 1004,
            SingpassSubjectId: "a9865837-7bd7-46ac-bef4-42a76a946424",
            Nric: "S8979373D",
            FullName: "Aisha Tan",
            DateOfBirth: new DateOnly(2007, 3, 18),
            StudentNumber: "DEMO-STU-0002",
            LevelCode: "SEC_5",
            ClassCode: "5B",
            Email: "aisha.tan@student.example.test",
            Mobile: "+6590000002",
            Address: "2 Demo Street, Singapore 000002"),
        new(
            LoginAccountId: 1005,
            PersonId: 2003,
            EnrollmentId: 3003,
            EducationAccountId: 4003,
            UserAccessScopeId: 1005,
            SingpassSubjectId: "f4b70aea-d639-4b79-b8d9-8ace5875f6b1",
            Nric: "S8116474F",
            FullName: "Benjamin Lee",
            DateOfBirth: new DateOnly(2006, 9, 24),
            StudentNumber: "DEMO-STU-0003",
            LevelCode: "ITE_Y1",
            ClassCode: "IT1A",
            Email: "benjamin.lee@student.example.test",
            Mobile: "+6590000003",
            Address: "3 Demo Street, Singapore 000003"),
        new(
            LoginAccountId: 1006,
            PersonId: 2004,
            EnrollmentId: 3004,
            EducationAccountId: 4004,
            UserAccessScopeId: 1006,
            SingpassSubjectId: "2135fe5c-d07b-49d3-b960-aabb0ff2e05a",
            Nric: "F9477325W",
            FullName: "Chloe Fernandez",
            DateOfBirth: new DateOnly(2005, 11, 2),
            StudentNumber: "DEMO-STU-0004",
            LevelCode: "POLY_Y2",
            ClassCode: "P2C",
            Email: "chloe.fernandez@student.example.test",
            Mobile: "+6590000004",
            Address: "4 Demo Street, Singapore 000004")
    ];
}

internal sealed record MockPassStudentSeed(
    long LoginAccountId,
    long PersonId,
    long EnrollmentId,
    long EducationAccountId,
    long UserAccessScopeId,
    string SingpassSubjectId,
    string Nric,
    string FullName,
    DateOnly DateOfBirth,
    string StudentNumber,
    string LevelCode,
    string ClassCode,
    string Email,
    string Mobile,
    string Address);
