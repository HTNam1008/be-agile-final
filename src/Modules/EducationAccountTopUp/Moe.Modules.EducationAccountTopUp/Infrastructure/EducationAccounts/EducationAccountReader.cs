using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;
using Moe.Modules.EducationAccountTopUp.Application.Interest;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.EducationAccounts;

internal sealed class EducationAccountReader(
    MoeDbContext dbContext,
    Microsoft.Extensions.Options.IOptions<EducationAccountInterestOptions> interestOptions)
    : IEducationAccountReader, IEducationAccountInterestHistoryReader
{
    public async Task<MyEducationAccountDto?> GetMyEducationAccountAsync(long personId, CancellationToken cancellationToken = default)
    {
        MyEducationAccountDto? account = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId)
            .Select(x => new MyEducationAccountDto(
                x.Id,
                x.PersonId,
                x.AccountNumber,
                CurrencyCodes.SingaporeDollar,
                x.StatusCode,
                x.CachedBalance,
                0m,
                x.CachedBalance,
                x.OpenedAtUtc,
                x.OpeningModeCode,
                x.OpeningRemarks,
                x.PendingClosureAtUtc,
                x.ClosedAtUtc,
                new MyEducationAccountTransactionsPage(
                    Array.Empty<MyEducationAccountTransactionDto>(),
                    1,
                    10,
                    0)
            ))
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        MyEducationAccountTransactionsPage transactions =
            await GetTransactionsAsync(personId, 1, 10, null, null, null, cancellationToken)
            ?? new MyEducationAccountTransactionsPage(
                Array.Empty<MyEducationAccountTransactionDto>(),
                1,
                10,
                0);

        return account with { Transactions = transactions };
    }

    public async Task<MyEducationAccountTransactionsPage?> GetTransactionsAsync(
        long personId,
        int page,
        int pageSize,
        string? category,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default)
    {
        long? educationAccountId = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(account => account.PersonId == personId)
            .Select(account => (long?)account.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (educationAccountId is null)
        {
            return null;
        }

        IQueryable<AccountTransaction> query = dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.EducationAccountId == educationAccountId.Value);

        query = ApplyCategoryFilter(query, category);

        long totalCount = await query.LongCountAsync(cancellationToken);

        AccountTransaction[] transactions = await ApplyTransactionSort(query, sortBy, sortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        MyEducationAccountTransactionDto[] items = transactions
            .Select(transaction => new MyEducationAccountTransactionDto(
                transaction.Id,
                transaction.TransactionTypeCode,
                ToCategory(transaction.TransactionTypeCode, transaction.ReferenceTypeCode, transaction.ReversalOfTransactionId),
                transaction.Amount,
                transaction.BalanceAfter,
                transaction.TransactionAtUtc,
                transaction.ReferenceTypeCode,
                transaction.ReferenceId,
                transaction.ReversalOfTransactionId,
                transaction.ReversalOfTransactionId is not null,
                transaction.Description))
            .ToArray();

        return new MyEducationAccountTransactionsPage(items, page, pageSize, totalCount);
    }

    private static IQueryable<AccountTransaction> ApplyTransactionSort(
        IQueryable<AccountTransaction> query,
        string? sortBy,
        string? sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        string key = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;

        return key switch
        {
            "transactionatutc" => descending
                ? query.OrderByDescending(x => x.TransactionAtUtc).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.TransactionAtUtc).ThenBy(x => x.Id),
            "category" => descending
                ? query.OrderByDescending(x => x.TransactionTypeCode).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.TransactionTypeCode).ThenBy(x => x.Id),
            "description" => descending
                ? query.OrderByDescending(x => x.Description).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Description).ThenBy(x => x.Id),
            "amount" => descending
                ? query.OrderByDescending(x => x.Amount).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.Amount).ThenBy(x => x.Id),
            "balanceafter" => descending
                ? query.OrderByDescending(x => x.BalanceAfter).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.BalanceAfter).ThenBy(x => x.Id),
            _ => query.OrderByDescending(transaction => transaction.TransactionAtUtc).ThenByDescending(transaction => transaction.Id)
        };
    }

    private static IQueryable<AccountTransaction> ApplyCategoryFilter(
        IQueryable<AccountTransaction> query,
        string? category)
    {
        return category switch
        {
            "TOP_UP" => query.Where(transaction =>
                transaction.TransactionTypeCode == "CREDIT" &&
                transaction.ReferenceTypeCode == "TOPUP"),
            "PAYMENT" => query.Where(transaction =>
                transaction.TransactionTypeCode == "PAYMENT" ||
                transaction.ReferenceTypeCode == "PAYMENT_PART"),
            "REFUND" => query.Where(transaction =>
                transaction.TransactionTypeCode == "REFUND" ||
                transaction.ReferenceTypeCode == "ENROLLMENT_REFUND"),
            "REVERSAL" => query.Where(transaction =>
                transaction.ReversalOfTransactionId != null),
            "INTEREST" => query.Where(transaction =>
                transaction.TransactionTypeCode == EducationAccountInterestCodes.TransactionTypeCode &&
                transaction.ReferenceTypeCode == EducationAccountInterestCodes.ReferenceTypeCode),
            _ => query
        };
    }

    private static string ToCategory(
        string transactionTypeCode,
        string referenceTypeCode,
        long? reversalOfTransactionId)
    {
        if (transactionTypeCode == "CREDIT" && referenceTypeCode == "TOPUP")
        {
            return "TOP_UP";
        }

        if (transactionTypeCode == "PAYMENT" || referenceTypeCode == "PAYMENT_PART")
        {
            return "PAYMENT";
        }

        if (transactionTypeCode == "REFUND" || referenceTypeCode == "ENROLLMENT_REFUND")
        {
            return "REFUND";
        }

        if (transactionTypeCode == EducationAccountInterestCodes.TransactionTypeCode
            && referenceTypeCode == EducationAccountInterestCodes.ReferenceTypeCode)
        {
            return EducationAccountInterestCodes.Category;
        }

        return reversalOfTransactionId is not null ? "REVERSAL" : "OTHER";
    }

    public async Task<EducationAccountInterestHistoryResponse?> GetMyInterestHistoryAsync(
        long personId,
        CancellationToken cancellationToken = default)
    {
        long? educationAccountId = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(account => account.PersonId == personId)
            .Select(account => (long?)account.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (educationAccountId is null)
        {
            return null;
        }

        EducationAccountInterestHistoryItem[] items = await dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .Where(transaction =>
                transaction.EducationAccountId == educationAccountId.Value &&
                transaction.TransactionTypeCode == EducationAccountInterestCodes.TransactionTypeCode &&
                transaction.ReferenceTypeCode == EducationAccountInterestCodes.ReferenceTypeCode)
            .OrderBy(transaction => transaction.ReferenceId)
            .ThenBy(transaction => transaction.TransactionAtUtc)
            .Select(transaction => new EducationAccountInterestHistoryItem(
                (int)(transaction.ReferenceId ?? transaction.TransactionAtUtc.Year - 1),
                transaction.BalanceAfter - transaction.Amount,
                transaction.Amount,
                transaction.BalanceAfter,
                transaction.TransactionAtUtc))
            .ToArrayAsync(cancellationToken);

        return new EducationAccountInterestHistoryResponse(
            AnnualInterestRate: interestOptions.Value.AnnualRate,
            CurrencyCode: CurrencyCodes.SingaporeDollar,
            Items: items);
    }
}
