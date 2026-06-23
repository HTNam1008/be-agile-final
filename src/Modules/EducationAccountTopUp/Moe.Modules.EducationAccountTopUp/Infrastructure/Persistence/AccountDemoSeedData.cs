namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal static partial class AccountDemoSeedData
{
    public static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public const long HqAdminLoginAccountId = 1001;
}

internal sealed record DemoEducationAccountSeed(
    long EducationAccountId,
    long PersonId,
    string AccountNumber);
