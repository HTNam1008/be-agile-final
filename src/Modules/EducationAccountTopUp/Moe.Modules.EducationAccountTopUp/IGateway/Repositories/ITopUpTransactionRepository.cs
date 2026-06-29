using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface ITopUpTransactionRepository
{
    Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<TopUpTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<TopUpTransaction?> GetByRunAndAccountAsync(
        long topUpRunId,
        long educationAccountId,
        CancellationToken cancellationToken = default);

    Task<List<TopUpTransaction>> GetByRunIdAsync(
        long topUpRunId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(
        long topUpRunId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalDisbursedForCampaignAsync(
        long campaignId,
        CancellationToken cancellationToken = default);

    Task<bool> TryReserveBudgetAsync(
        long campaignId,
        decimal requestedAmount,
        decimal budgetCap,
        CancellationToken cancellationToken = default);

    Task<List<TopUpTransaction>> GetByAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken = default);

    Task<(List<TopUpTransaction> Transactions, long TotalCount)> GetByAccountIdPagedAsync(
        long educationAccountId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<long> CountByAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken = default);

    void Add(TopUpTransaction transaction);

    Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default);
}
