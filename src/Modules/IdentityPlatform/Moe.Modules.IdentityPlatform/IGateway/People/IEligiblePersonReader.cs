namespace Moe.Modules.IdentityPlatform.IGateway.People;

public interface IEligiblePersonReader
{
    Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
        DateOnly today,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<long>> FindPersonIdsAgedAtLeastAsync(
        IReadOnlyCollection<long> personIds,
        int minAge,
        DateOnly today,
        CancellationToken cancellationToken);
}
