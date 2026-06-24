using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.Lifecycle.RunHistory;

public sealed record GetEducationAccountLifecycleRunDetailQuery(
    long RunId) : IQuery<EducationAccountLifecycleRunDetail>;

public sealed record EducationAccountLifecycleRunDetail(
    long RunId,
    DateOnly RunDateUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string TriggerTypeCode,
    string StatusCode,
    int OpenedCount,
    int ClosedCount,
    string? ErrorMessage,
    IReadOnlyList<EducationAccountLifecycleRunDetailItem> Items);

public sealed record EducationAccountLifecycleRunDetailItem(
    long ItemId,
    long PersonId,
    string? FullName,
    string? MaskedNric,
    string? SchoolName,
    long EducationAccountId,
    string AccountNumber,
    string ActionCode,
    DateTimeOffset OccurredAtUtc);

internal sealed class GetEducationAccountLifecycleRunDetailHandler(
    IEducationAccountLifecycleHistoryReader historyReader,
    ILifecyclePersonDisplayGateway people)
    : IQueryHandler<GetEducationAccountLifecycleRunDetailQuery, EducationAccountLifecycleRunDetail>
{
    public async Task<Result<EducationAccountLifecycleRunDetail>> Handle(
        GetEducationAccountLifecycleRunDetailQuery query,
        CancellationToken cancellationToken)
    {
        EducationAccountLifecycleRunDetailProjection? projection =
            await historyReader.GetRunDetailAsync(query.RunId, cancellationToken);
        if (projection is null)
        {
            return Result<EducationAccountLifecycleRunDetail>.Failure(
                EducationAccountLifecycleRunHistoryErrors.RunNotFound);
        }

        IReadOnlyCollection<LifecyclePersonDisplay> displays =
            await people.FindByPersonIdsAsync(
                projection.Items.Select(x => x.PersonId).Distinct().ToArray(),
                cancellationToken);
        Dictionary<long, LifecyclePersonDisplay> displayByPersonId =
            displays.ToDictionary(x => x.PersonId);

        EducationAccountLifecycleRunDetailItem[] items = projection.Items
            .Select(x =>
            {
                displayByPersonId.TryGetValue(x.PersonId, out LifecyclePersonDisplay? display);
                return new EducationAccountLifecycleRunDetailItem(
                    x.ItemId,
                    x.PersonId,
                    display?.FullName,
                    display?.MaskedNric,
                    display?.SchoolName,
                    x.EducationAccountId,
                    x.AccountNumber,
                    x.ActionCode,
                    x.OccurredAtUtc);
            })
            .ToArray();

        return Result<EducationAccountLifecycleRunDetail>.Success(
            new EducationAccountLifecycleRunDetail(
                projection.RunId,
                projection.RunDateUtc,
                projection.StartedAtUtc,
                projection.CompletedAtUtc,
                projection.TriggerTypeCode,
                projection.StatusCode,
                projection.OpenedCount,
                projection.ClosedCount,
                projection.ErrorMessage,
                items));
    }
}

internal static class EducationAccountLifecycleRunHistoryErrors
{
    public static readonly Error RunNotFound = new(
        "EDUCATION_ACCOUNT_LIFECYCLE.RUN_NOT_FOUND",
        "The Education Account lifecycle run was not found.");
}
