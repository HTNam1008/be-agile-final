namespace Moe.Modules.IdentityPlatform.IGateway.People;

public interface IEligiblePersonReader
{
    Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
        DateOnly today,
        CancellationToken cancellationToken);
}
