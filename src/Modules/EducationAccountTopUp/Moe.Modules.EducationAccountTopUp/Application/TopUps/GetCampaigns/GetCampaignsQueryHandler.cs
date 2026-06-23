using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.DTOs;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

internal sealed class GetCampaignsQueryHandler(
    ITopUpCampaignReader reader,
    IAdminAccessControl adminAccess) : IQueryHandler<GetCampaignsQuery, IReadOnlyList<CampaignListItem>>
{
    public async Task<Result<IReadOnlyList<CampaignListItem>>> Handle(
        GetCampaignsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CampaignListItem> result = await reader.GetCampaignsAsync(
            adminAccess.IsHqAdmin ? null : adminAccess.ScopedOrganizationIds,
            cancellationToken);

        return Result<IReadOnlyList<CampaignListItem>>.Success(result);
    }
}
