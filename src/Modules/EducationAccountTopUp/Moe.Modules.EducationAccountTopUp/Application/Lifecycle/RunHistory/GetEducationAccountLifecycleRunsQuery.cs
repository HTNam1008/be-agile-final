using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle.RunHistory;

public sealed record GetEducationAccountLifecycleRunsQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int PageSize) : IQuery<PageResponse<EducationAccountLifecycleRunListItem>>;

public sealed record EducationAccountLifecycleRunListItem(
    long RunId,
    DateOnly RunDateUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string TriggerTypeCode,
    string StatusCode,
    int OpenedCount,
    int ClosedCount,
    string? ErrorMessage);

internal sealed class GetEducationAccountLifecycleRunsHandler(
    IEducationAccountLifecycleHistoryReader historyReader)
    : IQueryHandler<GetEducationAccountLifecycleRunsQuery, PageResponse<EducationAccountLifecycleRunListItem>>
{
    public async Task<Result<PageResponse<EducationAccountLifecycleRunListItem>>> Handle(
        GetEducationAccountLifecycleRunsQuery query,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(query.Page, 1);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        HistoryPage<EducationAccountLifecycleRunProjection> historyPage =
            await historyReader.ListRunsAsync(
                query.FromDate,
                query.ToDate,
                page,
                pageSize,
                cancellationToken);

        EducationAccountLifecycleRunListItem[] items = historyPage.Items
            .Select(x => new EducationAccountLifecycleRunListItem(
                x.RunId,
                x.RunDateUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.TriggerTypeCode,
                x.StatusCode,
                x.OpenedCount,
                x.ClosedCount,
                x.ErrorMessage))
            .ToArray();

        return Result<PageResponse<EducationAccountLifecycleRunListItem>>.Success(
            new PageResponse<EducationAccountLifecycleRunListItem>(
                items,
                page,
                pageSize,
                historyPage.TotalCount));
    }
}
