using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

internal sealed class GetCampaignsQueryHandler(
    ITopUpCampaignRepository campaigns,
    IAdminAccessControl adminAccess) : IQueryHandler<GetCampaignsQuery, IReadOnlyList<CampaignListItem>>
{
    public async Task<Result<IReadOnlyList<CampaignListItem>>> Handle(
        GetCampaignsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CampaignListItem> result = await campaigns.ListAsync(
            adminAccess.IsHqAdmin ? null : adminAccess.ScopedOrganizationIds,
            cancellationToken);

        return Result<IReadOnlyList<CampaignListItem>>.Success(result);
    }
}
