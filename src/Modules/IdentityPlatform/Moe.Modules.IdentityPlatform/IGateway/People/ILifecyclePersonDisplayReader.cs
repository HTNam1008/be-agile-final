namespace Moe.Modules.IdentityPlatform.IGateway.People;

public sealed record LifecyclePersonDisplaySummary(
    long PersonId,
    string FullName,
    string MaskedNric,
    string? SchoolName);

public interface ILifecyclePersonDisplayReader
{
    Task<IReadOnlyCollection<LifecyclePersonDisplaySummary>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken);
}
