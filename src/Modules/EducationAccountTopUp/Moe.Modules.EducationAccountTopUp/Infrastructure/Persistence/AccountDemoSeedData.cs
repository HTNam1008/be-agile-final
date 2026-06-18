namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal static class AccountDemoSeedData
{
    public static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public const long SystemAdminLoginAccountId = 1001;
    public const long StudentPersonId = 2001;
    public const long TopUpStudentPersonIdOne = 2002;
    public const long TopUpStudentPersonIdTwo = 2003;
    public const long TopUpStudentPersonIdThree = 2004;
    public const long StudentEducationAccountId = 4001;
    public const long TopUpEducationAccountIdOne = 4002;
    public const long TopUpEducationAccountIdTwo = 4003;
    public const long TopUpEducationAccountIdThree = 4004;
}
