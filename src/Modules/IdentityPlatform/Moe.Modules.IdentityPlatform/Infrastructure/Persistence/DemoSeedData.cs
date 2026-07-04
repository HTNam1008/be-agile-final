namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal static partial class DemoSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const long HqAdminLoginAccountId = 1001;
    public const string TenantId = "ea71ddeb-596c-4034-84d4-d65f91edc14a";
    public const string EntraIssuer = "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/";
    public const string HqAdminObjectId = "731f2a50-4fa7-4530-9294-1a5b912daf31";
    public const string MockPassIssuer = "http://localhost:5156/singpass/v3/fapi";

    static DemoSeedData()
    {
        const int expectedSchoolCount = 5;
        const int expectedStudentsPerSchool = 10;
        const int expectedStudentCount = expectedSchoolCount * expectedStudentsPerSchool;

        if (MockSchools.Count != expectedSchoolCount || MockPassStudents.Count != expectedStudentCount)
        {
            throw new InvalidOperationException(
                $"Demo seed must contain exactly {expectedSchoolCount} schools and {expectedStudentCount} students.");
        }

        long[] schoolIds = MockSchools.Select(x => x.OrganizationId).ToArray();
        bool hasInvalidSchoolDistribution = MockPassStudents
            .GroupBy(x => x.OrganizationId)
            .Any(group => !schoolIds.Contains(group.Key) || group.Count() != expectedStudentsPerSchool)
            || schoolIds.Any(schoolId => MockPassStudents.Count(x => x.OrganizationId == schoolId) != expectedStudentsPerSchool);

        if (hasInvalidSchoolDistribution)
        {
            throw new InvalidOperationException(
                $"Demo seed must assign exactly {expectedStudentsPerSchool} students to each school.");
        }

        EnsureUnique(MockPassStudents.Select(x => x.LoginAccountId), nameof(MockPassStudentSeed.LoginAccountId));
        EnsureUnique(MockPassStudents.Select(x => x.PersonId), nameof(MockPassStudentSeed.PersonId));
        EnsureUnique(MockPassStudents.Select(x => x.EnrollmentId), nameof(MockPassStudentSeed.EnrollmentId));
        EnsureUnique(MockPassStudents.Select(x => x.EducationAccountId), nameof(MockPassStudentSeed.EducationAccountId));
        EnsureUnique(MockPassStudents.Select(x => x.UserAccessScopeId), nameof(MockPassStudentSeed.UserAccessScopeId));
        EnsureUnique(MockPassStudents.Select(x => x.SingpassSubjectId), nameof(MockPassStudentSeed.SingpassSubjectId));
        EnsureUnique(MockPassStudents.Select(x => x.Nric), nameof(MockPassStudentSeed.Nric));
        EnsureUnique(MockPassStudents.Select(x => x.StudentNumber), nameof(MockPassStudentSeed.StudentNumber));
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string fieldName)
    {
        if (values.Distinct().Count() != MockPassStudents.Count)
        {
            throw new InvalidOperationException($"Demo student seed contains duplicate {fieldName} values.");
        }
    }
}

internal sealed record MockSchoolSeed(
    long OrganizationId,
    string SchoolCode,
    string SchoolName,
    string MockPassSchoolCode);

internal sealed record MockPassStudentSeed(
    long LoginAccountId,
    long PersonId,
    long EnrollmentId,
    long EducationAccountId,
    long UserAccessScopeId,
    long OrganizationId,
    string SchoolCode,
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
