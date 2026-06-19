using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

internal sealed class GetCampaignsQueryHandler(
    ITopUpCampaignRepository campaigns,
    ICurrentUser currentUser) : IQueryHandler<GetCampaignsQuery, IReadOnlyList<CampaignListItem>>
{
    public async Task<Result<IReadOnlyList<CampaignListItem>>> Handle(
        GetCampaignsQuery query,
        CancellationToken cancellationToken)
    {
        // Build the set of org IDs this user can see.
        // OrganizationUnitIds is the full collection; OrganizationUnitId is the primary.
        var accessibleOrgIds = new HashSet<long>(currentUser.OrganizationUnitIds);
        if (currentUser.OrganizationUnitId.HasValue)
            accessibleOrgIds.Add(currentUser.OrganizationUnitId.Value);

        IReadOnlyList<CampaignListItem> result = await campaigns.ListAsync(
            accessibleOrgIds,
            cancellationToken);

        return Result<IReadOnlyList<CampaignListItem>>.Success(result);
    }
}
