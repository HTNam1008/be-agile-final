using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.CampaignHistory;

internal sealed class GetCampaignHistoryHandler(
    ITopUpAccessScopeResolver accessScopeResolver,
    ITopUpHistoryReader historyReader)
    : IQueryHandler<GetCampaignHistoryQuery, PageResponse<CampaignHistoryItem>>
{
    public async Task<Result<PageResponse<CampaignHistoryItem>>> Handle(
        GetCampaignHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result<TopUpAccessScope> accessResult =
            accessScopeResolver.Resolve(query.Filter.OrganizationId);

        if (accessResult.IsFailure)
        {
            return Result<PageResponse<CampaignHistoryItem>>.Failure(accessResult.Error);
        }

        HistoryPage<CampaignHistoryProjection> page = await historyReader.GetCampaignHistoryAsync(
            query.Filter,
            accessResult.Value,
            query.Page,
            query.PageSize,
            cancellationToken);

        CampaignHistoryItem[] items = page.Items
            .Select(x => new CampaignHistoryItem(
                x.CampaignId,
                x.CampaignCode,
                x.CampaignName,
                x.OrganizationId,
                x.Version,
                x.ScheduleType,
                x.StartDate,
                x.EndDate,
                x.NextRunAtUtc,
                x.Status,
                x.CreatedByUserId,
                x.CreatedAtUtc,
                x.UpdatedByUserId,
                x.UpdatedAtUtc))
            .ToArray();

        return Result<PageResponse<CampaignHistoryItem>>.Success(
            new PageResponse<CampaignHistoryItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }
}
