using Moe.Modules.EducationAccountTopUp.Application.History;

namespace Moe.Modules.EducationAccountTopUp.IGateway.History;

internal interface IEducationAccountLifecycleHistoryReader
{
    Task<HistoryPage<EducationAccountLifecycleRunProjection>> ListRunsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<EducationAccountLifecycleRunDetailProjection?> GetRunDetailAsync(
        long runId,
        CancellationToken cancellationToken);
}

internal sealed record EducationAccountLifecycleRunProjection(
    long RunId,
    DateOnly RunDateUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string TriggerTypeCode,
    string StatusCode,
    int OpenedCount,
    int ClosedCount,
    string? ErrorMessage);

internal sealed record EducationAccountLifecycleRunDetailProjection(
    long RunId,
    DateOnly RunDateUtc,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string TriggerTypeCode,
    string StatusCode,
    int OpenedCount,
    int ClosedCount,
    string? ErrorMessage,
    IReadOnlyList<EducationAccountLifecycleRunItemProjection> Items);

internal sealed record EducationAccountLifecycleRunItemProjection(
    long ItemId,
    long PersonId,
    long EducationAccountId,
    string AccountNumber,
    string ActionCode,
    DateTimeOffset OccurredAtUtc);
