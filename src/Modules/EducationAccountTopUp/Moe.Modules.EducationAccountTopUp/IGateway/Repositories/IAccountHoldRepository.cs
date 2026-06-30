namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

internal interface IAccountHoldRepository
{
    Task<bool> HasPendingHoldAsync(
        long educationAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
