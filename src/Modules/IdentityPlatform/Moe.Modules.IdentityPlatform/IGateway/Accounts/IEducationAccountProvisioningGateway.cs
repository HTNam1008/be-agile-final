namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public sealed record EducationAccountProvisioningResult(
    long? EducationAccountId,
    string? AccountNumber,
    bool IsAccountHolder)
{
    public bool Created { get; init; }
}

public interface IEducationAccountProvisioningGateway
{
    Task<EducationAccountProvisioningResult> EnsureAccountForStudentAsync(
        long personId,
        long openedByUserAccountId,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken,
        bool saveChanges = true);

    Task<bool> HasAccountAsync(long personId, CancellationToken cancellationToken);

    Task<bool> HasActiveAccountAsync(long personId, CancellationToken cancellationToken);
}
