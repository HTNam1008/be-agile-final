using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.People;

namespace Moe.StudentFinance.Api.CompositionRoot;

internal sealed class EligiblePersonLookupGatewayAdapter(IEligiblePersonReader reader)
    : IEligiblePersonLookupGateway
{
    public Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        return reader.FindEligibleForEducationAccountAsync(today, cancellationToken);
    }

    public Task<IReadOnlyCollection<long>> FindPersonIdsAgedAtLeastAsync(
        IReadOnlyCollection<long> personIds,
        int minAge,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        return reader.FindPersonIdsAgedAtLeastAsync(personIds, minAge, today, cancellationToken);
    }
}
