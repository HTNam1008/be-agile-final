using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;

internal sealed class GetCampaignsQueryHandler(
    ITopUpCampaignReader reader,
    IAdminAccessControl adminAccess) : IQueryHandler<GetCampaignsQuery, CampaignListResult>
{
    public async Task<Result<CampaignListResult>> Handle(
        GetCampaignsQuery query,
        CancellationToken cancellationToken)
    {
        var dateFrom = query.DateFrom != null ? DateOnly.Parse(query.DateFrom) : (DateOnly?)null;
        var dateTo = query.DateTo != null ? DateOnly.Parse(query.DateTo) : (DateOnly?)null;

        CampaignListResult result = await reader.GetCampaignsAsync(
            adminAccess.IsHqAdmin ? null : adminAccess.ScopedOrganizationIds,
            query.PageNumber,
            query.PageSize,
            query.Search,
            query.Status,
            dateFrom,
            dateTo,
            cancellationToken);

        return Result<CampaignListResult>.Success(result);
    }
}
