using Moe.Modules.EducationAccountTopUp.Application.History;

namespace Moe.Modules.EducationAccountTopUp.IGateway.History;

internal interface ITopUpTransactionReader
{
    Task<TransactionHistoryPage> GetCampaignTransactionsAsync(
        long campaignId,
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TransactionHistoryPage> GetAccountTransactionsAsync(
        long educationAccountId,
        TopUpHistoryFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record TransactionHistoryPage(
    IReadOnlyList<TransactionHistoryProjection> Items,
    long TotalCount);

internal sealed record TransactionHistoryProjection(
    long TransactionId,
    long RunId,
    long EducationAccountId,
    decimal Amount,
    string StatusCode,
    string? Reason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime RunDateUtc,
    string CampaignName);
