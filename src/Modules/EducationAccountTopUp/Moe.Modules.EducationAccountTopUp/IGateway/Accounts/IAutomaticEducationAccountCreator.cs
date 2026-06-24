namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public sealed record AutomaticEducationAccountCreationResult(
    long EducationAccountId,
    string AccountNumber,
    bool Created);

public interface IAutomaticEducationAccountCreator
{
    Task<AutomaticEducationAccountCreationResult> EnsureCreatedAsync(
        long personId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken);
}
