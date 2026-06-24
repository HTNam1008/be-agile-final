namespace Moe.Modules.EducationAccountTopUp.IGateway.People;

public interface IEligiblePersonLookupGateway
{
    Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
        DateOnly today,
        CancellationToken cancellationToken);
}
