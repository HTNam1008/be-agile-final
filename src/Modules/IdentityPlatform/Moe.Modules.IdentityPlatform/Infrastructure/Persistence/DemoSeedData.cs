namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal static partial class DemoSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public const long HqAdminLoginAccountId = 1001;
    public const string TenantId = "ea71ddeb-596c-4034-84d4-d65f91edc14a";
    public const string EntraIssuer = "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/";
    public const string HqAdminObjectId = "731f2a50-4fa7-4530-9294-1a5b912daf31";
    public const string MockPassIssuer = "http://localhost:5156/singpass/v3/fapi";
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
