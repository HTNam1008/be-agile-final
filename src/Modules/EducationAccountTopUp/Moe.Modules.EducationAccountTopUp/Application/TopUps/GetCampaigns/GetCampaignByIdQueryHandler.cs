using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

internal sealed class GetCampaignByIdQueryHandler(
    ITopUpCampaignReader reader) : IQueryHandler<GetCampaignByIdQuery, CampaignListItem?>
{
    public async Task<Result<CampaignListItem?>> Handle(
        GetCampaignByIdQuery query,
        CancellationToken cancellationToken)
    {
        CampaignListItem? result = await reader.GetByIdAsync(query.Id, cancellationToken);
        return Result<CampaignListItem?>.Success(result);
    }
}
