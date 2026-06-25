using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.People;

namespace Moe.StudentFinance.Api.CompositionRoot;

internal sealed class LifecyclePersonDisplayGatewayAdapter(ILifecyclePersonDisplayReader reader)
    : ILifecyclePersonDisplayGateway
{
    public async Task<IReadOnlyCollection<LifecyclePersonDisplay>> FindByPersonIdsAsync(
        IReadOnlyCollection<long> personIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<LifecyclePersonDisplaySummary> people =
            await reader.FindByPersonIdsAsync(personIds, cancellationToken);

        return people
            .Select(x => new LifecyclePersonDisplay(
                x.PersonId,
                x.FullName,
                x.MaskedNric,
                x.SchoolName))
            .ToArray();
    }
}
