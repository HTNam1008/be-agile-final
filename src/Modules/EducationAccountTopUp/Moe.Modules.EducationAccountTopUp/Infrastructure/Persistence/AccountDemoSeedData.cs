namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

internal static partial class AccountDemoSeedData
{
    public static readonly DateTimeOffset SeededAtUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public const long HqAdminLoginAccountId = 1001;

    static AccountDemoSeedData()
    {
        const int expectedAccountCount = 50;
        const long firstExpectedPersonId = 2001;
        const long lastExpectedPersonId = 2050;

        if (DemoStudentAccounts.Count != expectedAccountCount)
        {
            throw new InvalidOperationException($"Demo education account seed must contain exactly {expectedAccountCount} accounts.");
        }

        EnsureUnique(DemoStudentAccounts.Select(x => x.EducationAccountId), nameof(DemoEducationAccountSeed.EducationAccountId));
        EnsureUnique(DemoStudentAccounts.Select(x => x.PersonId), nameof(DemoEducationAccountSeed.PersonId));
        EnsureUnique(DemoStudentAccounts.Select(x => x.AccountNumber), nameof(DemoEducationAccountSeed.AccountNumber));

        long[] expectedPersonIds = Enumerable.Range(
                (int)firstExpectedPersonId,
                (int)(lastExpectedPersonId - firstExpectedPersonId + 1))
            .Select(x => (long)x)
            .ToArray();

        if (!DemoStudentAccounts.Select(x => x.PersonId).Order().SequenceEqual(expectedPersonIds))
        {
            throw new InvalidOperationException($"Demo education account seed must contain one account for every PersonId from {firstExpectedPersonId} to {lastExpectedPersonId}.");
        }
    }

    private static void EnsureUnique<T>(IEnumerable<T> values, string fieldName)
    {
        if (values.Distinct().Count() != DemoStudentAccounts.Count)
        {
            throw new InvalidOperationException($"Demo education account seed contains duplicate {fieldName} values.");
        }
    }
}

internal sealed record DemoEducationAccountSeed(
    long EducationAccountId,
    long PersonId,
    string AccountNumber);
