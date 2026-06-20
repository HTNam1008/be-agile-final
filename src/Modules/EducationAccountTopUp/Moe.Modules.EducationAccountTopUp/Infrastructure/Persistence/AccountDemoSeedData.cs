namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal static class AccountDemoSeedData
{
    public static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public const long HqAdminLoginAccountId = 1001;
    public const long StudentPersonId = 2001;
    public const long StudentEducationAccountId = 4001;

    public static readonly IReadOnlyList<DemoEducationAccountSeed> DemoStudentAccounts =
    [
        new(4001, 2001, "EA-DEMO-0001"),
        new(4002, 2002, "EA-DEMO-0002"),
        new(4003, 2003, "EA-DEMO-0003"),
        new(4004, 2004, "EA-DEMO-0004")
    ];
}

internal sealed record DemoEducationAccountSeed(
    long EducationAccountId,
    long PersonId,
    string AccountNumber);
