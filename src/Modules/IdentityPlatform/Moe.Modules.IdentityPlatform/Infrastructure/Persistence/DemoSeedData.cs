namespace Moe.Modules.IdentityPlatform.Infrastructure.Persistence;

internal static class DemoSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly StudentDateOfBirth = new(2008, 5, 12);

    public const long SystemAdminLoginAccountId = 1001;
    public const long SchoolAdminLoginAccountId = 1002;
    public const long StudentLoginAccountId = 1003;
    public const long StudentPersonId = 2001;
    public const long TopUpStudentPersonIdOne = 2002;
    public const long TopUpStudentPersonIdTwo = 2003;
    public const long TopUpStudentPersonIdThree = 2004;
    public const long StudentSchoolEnrollmentId = 3001;
    public const long TopUpStudentSchoolEnrollmentIdOne = 3002;
    public const long TopUpStudentSchoolEnrollmentIdTwo = 3003;
    public const long TopUpStudentSchoolEnrollmentIdThree = 3004;
    public const long StudentEducationAccountId = 4001;

    public const string TenantId = "ea71ddeb-596c-4034-84d4-d65f91edc14a";
    public const string EntraIssuer = "https://sts.windows.net/ea71ddeb-596c-4034-84d4-d65f91edc14a/";
    public const string SystemAdminObjectId = "731f2a50-4fa7-4530-9294-1a5b912daf31";
    public const string SchoolAdminObjectId = "00000000-0000-0000-0000-000000000222";

    public const string MockPassIssuer = "http://localhost:5156/singpass/v3/fapi";
    public const string MockPassSubject = "ef39a074-b64d-4990-a937-6f80772e2bb8";
    public const string MockPassNric = "S1234567A";
}
