namespace Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;

internal static class EducationAccountNumberFactory
{
    public static string ForPerson(long personId) => $"PSEA-{personId:D8}";
}
