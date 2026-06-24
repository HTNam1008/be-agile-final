namespace Moe.Modules.EducationAccountTopUp.IGateway.People;

public sealed record LifecyclePersonDisplay(
    long PersonId,
    string FullName,
    string MaskedNric);

public interface ILifecyclePersonDisplayGateway
{
    Task<IReadOnlyCollection<LifecyclePersonDisplay>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken);
}
