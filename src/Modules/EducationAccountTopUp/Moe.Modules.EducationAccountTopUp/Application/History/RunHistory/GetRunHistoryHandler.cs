using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.RunHistory;

internal sealed class GetRunHistoryHandler(
    ITopUpAccessScopeResolver accessScopeResolver,
    ITopUpHistoryReader historyReader)
    : IQueryHandler<GetRunHistoryQuery, PageResponse<RunHistoryItem>>
{
    public async Task<Result<PageResponse<RunHistoryItem>>> Handle(
        GetRunHistoryQuery query,
        CancellationToken cancellationToken)
    {
        Result<TopUpAccessScope> accessResult =
            accessScopeResolver.Resolve(query.Filter.OrganizationId);

        if (accessResult.IsFailure)
        {
            return Result<PageResponse<RunHistoryItem>>.Failure(accessResult.Error);
        }

        HistoryPage<RunHistoryProjection> page = await historyReader.GetRunHistoryAsync(
            query.Filter,
            accessResult.Value,
            query.Page,
            query.PageSize,
            cancellationToken);

        RunHistoryItem[] items = page.Items
            .Select(x => new RunHistoryItem(
                x.RunId,
                x.CampaignId,
                x.CampaignCode,
                x.CampaignName,
                x.OrganizationId,
                x.RunDateUtc,
                x.TriggerType,
                x.Status,
                x.MatchedCount,
                x.ProcessedCount,
                x.SucceededCount,
                x.FailedCount,
                x.TotalCredited,
                x.TriggeredByUserId,
                x.StartedAtUtc,
                x.CompletedAtUtc))
            .ToArray();

        return Result<PageResponse<RunHistoryItem>>.Success(
            new PageResponse<RunHistoryItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }
}
