using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;
using TopUpRun = Moe.Modules.EducationAccountTopUp.Domain.TopUps.TopUpRun;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpTransactionRepository(MoeDbContext dbContext) : ITopUpTransactionRepository
{
    public Task<TopUpTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<TopUpTransaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public Task<TopUpTransaction?> GetByRunAndAccountAsync(
        long topUpRunId,
        long educationAccountId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .SingleOrDefaultAsync(
                x => x.TopUpRunId == topUpRunId && x.EducationAccountId == educationAccountId,
                cancellationToken);
    }

    public Task<List<TopUpTransaction>> GetByRunIdAsync(
        long topUpRunId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .Where(x => x.TopUpRunId == topUpRunId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopUpTransaction>> GetPendingByRunIdPagedAsync(
        long topUpRunId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .Where(x => x.TopUpRunId == topUpRunId
                && x.TransactionStatusCode == TopUpTransactionStatusCodes.Pending)
            .OrderBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalDisbursedForCampaignAsync(
        long campaignId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .Where(x => dbContext.Set<TopUpRun>()
                .Any(r => r.Id == x.TopUpRunId && r.TopUpCampaignId == campaignId)
                && x.TransactionStatusCode == TopUpTransactionStatusCodes.Completed)
            .SumAsync(x => x.Amount, cancellationToken);
    }

    public async Task<bool> TryReserveBudgetAsync(
        long campaignId,
        decimal requestedAmount,
        decimal budgetCap,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                DECLARE @AlreadyDisbursed DECIMAL(18,2);
                SELECT @AlreadyDisbursed = ISNULL(SUM(t.Amount), 0)
                FROM TopUpTransactions t
                INNER JOIN TopUpRuns r ON t.TopUpRunId = r.Id
                WHERE r.TopUpCampaignId = @CampaignId
                AND t.TransactionStatusCode = 'COMPLETED';

                IF (@AlreadyDisbursed + @RequestedAmount <= @BudgetCap)
                BEGIN
                    SELECT 1;
                END
                ELSE
                BEGIN
                    SELECT 0;
                END";

            command.Parameters.Add(CreateParameter(command, "@CampaignId", campaignId));
            command.Parameters.Add(CreateParameter(command, "@RequestedAmount", requestedAmount));
            command.Parameters.Add(CreateParameter(command, "@BudgetCap", budgetCap));

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && Convert.ToInt32(result) == 1;
        }
        catch (InvalidOperationException)
        {
            decimal alreadyDisbursed = await GetTotalDisbursedForCampaignAsync(campaignId, cancellationToken);
            return alreadyDisbursed + requestedAmount <= budgetCap;
        }
    }

    private static System.Data.Common.DbParameter CreateParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        return parameter;
    }

    public Task<List<TopUpTransaction>> GetByAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .Where(x => x.EducationAccountId == educationAccountId)
            .OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<TopUpTransaction> Transactions, long TotalCount)> GetByAccountIdPagedAsync(
        long educationAccountId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .Where(x => x.EducationAccountId == educationAccountId);

        var totalCount = await query.LongCountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(x => x.CompletedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (transactions, totalCount);
    }

    public async Task<long> CountByAccountIdAsync(
        long educationAccountId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TopUpTransaction>()
            .AsNoTracking()
            .LongCountAsync(x => x.EducationAccountId == educationAccountId, cancellationToken);
    }

    public void Add(TopUpTransaction transaction)
    {
        dbContext.Set<TopUpTransaction>().Add(transaction);
    }

    public async Task AddAsync(TopUpTransaction transaction, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TopUpTransaction>().AddAsync(transaction, cancellationToken);
    }
}
