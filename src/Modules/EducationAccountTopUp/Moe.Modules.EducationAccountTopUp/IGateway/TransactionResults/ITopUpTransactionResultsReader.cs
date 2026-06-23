using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;

namespace Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;

internal interface ITopUpTransactionResultsReader
{
    Task<TransactionResultsPage> GetPageAsync(
        long runId,
        TopUpTransactionResultFilter filter,
        IReadOnlyCollection<long>? matchingEducationAccountIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record TransactionResultsPage(
    IReadOnlyList<TopUpTransactionResultProjection> Items,
    long TotalCount);

internal sealed record TopUpTransactionResultProjection(
    long TransactionId,
    long EducationAccountId,
    decimal Amount,
    string Status,
    string? Reason,
    long? AccountTransactionId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
