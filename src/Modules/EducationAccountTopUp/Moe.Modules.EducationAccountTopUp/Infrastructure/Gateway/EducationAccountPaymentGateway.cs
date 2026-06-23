using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class EducationAccountPaymentGateway(MoeDbContext dbContext)
    : IEducationAccountPaymentGateway
{
    public async Task<EducationAccountPaymentBalance?> GetAvailableBalanceAsync(
        long personId,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        await ReleaseExpiredHoldsAsync(now, cancellationToken);
        EducationAccount? account = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonId == personId && x.StatusCode == AccountStatuses.Active, cancellationToken);
        if (account is null) return null;
        decimal held = await dbContext.Set<AccountHold>()
            .Where(x =>
                x.EducationAccountId == account.Id &&
                x.HoldStatusCode == AccountHoldStatusCodes.Reserved &&
                x.ExpiresAtUtc > now)
            .SumAsync(x => x.HoldAmount, cancellationToken);
        return new(account.Id, account.CachedBalance, held, Math.Max(0m, account.CachedBalance - held), "SGD");
    }

    public async Task<long> ReserveAsync(
        long personId,
        long paymentPartId,
        decimal amount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        await ReleaseExpiredHoldsAsync(now, cancellationToken);
        EducationAccount account = await ActiveAccount(personId, cancellationToken);
        decimal held = await dbContext.Set<AccountHold>()
            .Where(x =>
                x.EducationAccountId == account.Id &&
                x.HoldStatusCode == AccountHoldStatusCodes.Reserved &&
                x.ExpiresAtUtc > now)
            .SumAsync(x => x.HoldAmount, cancellationToken);
        if (account.CachedBalance - held < amount)
            throw new InvalidOperationException("Education Account available balance is insufficient.");
        AccountHold hold = AccountHold.Reserve(account.Id, paymentPartId, amount, now, expiresAtUtc);
        await dbContext.Set<AccountHold>().AddAsync(hold, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return hold.Id;
    }

    public async Task CaptureAsync(
        long accountHoldId,
        long? actorLoginAccountId,
        CancellationToken cancellationToken)
    {
        AccountHold hold = await dbContext.Set<AccountHold>().SingleAsync(x => x.Id == accountHoldId, cancellationToken);
        EducationAccount account = await dbContext.Set<EducationAccount>().SingleAsync(x => x.Id == hold.EducationAccountId, cancellationToken);
        if (account.CachedBalance < hold.HoldAmount) throw new InvalidOperationException("Education Account balance is insufficient.");
        DateTime now = DateTime.UtcNow;
        AccountTransaction transaction = AccountTransaction.Create(
            account.Id, "PAYMENT", -hold.HoldAmount, "PAYMENT_PART", hold.PaymentPartId,
            $"PAYMENT-HOLD:{hold.Id}", account.CachedBalance, "Course billing payment",
            actorLoginAccountId, now);
        account.UpdateBalance(-hold.HoldAmount);
        await dbContext.Set<AccountTransaction>().AddAsync(transaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        hold.Capture(transaction.Id, now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(long accountHoldId, CancellationToken cancellationToken)
    {
        AccountHold hold = await dbContext.Set<AccountHold>().SingleAsync(x => x.Id == accountHoldId, cancellationToken);
        hold.Release(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> DebitImmediatelyAsync(
        long personId,
        long paymentPartId,
        decimal amount,
        long? actorLoginAccountId,
        CancellationToken cancellationToken)
    {
        EducationAccount account = await ActiveAccount(personId, cancellationToken);
        if (account.CachedBalance < amount) throw new InvalidOperationException("Education Account balance is insufficient.");
        AccountTransaction transaction = AccountTransaction.Create(
            account.Id, "PAYMENT", -amount, "PAYMENT_PART", paymentPartId,
            $"PAYMENT-PART:{paymentPartId}", account.CachedBalance, "Course billing payment",
            actorLoginAccountId, DateTime.UtcNow);
        account.UpdateBalance(-amount);
        await dbContext.Set<AccountTransaction>().AddAsync(transaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transaction.Id;
    }

    public async Task<long> CreditRefundAsync(
        long personId,
        long refundReferenceId,
        decimal amount,
        long? reversalOfTransactionId,
        string idempotencyKey,
        long? actorLoginAccountId,
        CancellationToken cancellationToken)
    {
        if (refundReferenceId <= 0 || amount <= 0m || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentOutOfRangeException(nameof(amount));

        AccountTransaction? existing = await dbContext.Set<AccountTransaction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is not null)
            return existing.Id;

        EducationAccount account = await ActiveAccount(personId, cancellationToken);
        DateTime now = DateTime.UtcNow;
        AccountTransaction transaction = AccountTransaction.Create(
            account.Id,
            "REFUND",
            amount,
            "ENROLLMENT_REFUND",
            refundReferenceId,
            idempotencyKey,
            account.CachedBalance,
            "Course enrollment refund",
            actorLoginAccountId,
            now,
            reversalOfTransactionId);
        account.UpdateBalance(amount);
        await dbContext.Set<AccountTransaction>().AddAsync(transaction, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transaction.Id;
    }

    private Task<EducationAccount> ActiveAccount(long personId, CancellationToken cancellationToken)
        => dbContext.Set<EducationAccount>().SingleAsync(
            x => x.PersonId == personId && x.StatusCode == AccountStatuses.Active,
            cancellationToken);

    private async Task ReleaseExpiredHoldsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        AccountHold[] expired = await dbContext.Set<AccountHold>()
            .Where(x =>
                x.HoldStatusCode == AccountHoldStatusCodes.Reserved &&
                x.ExpiresAtUtc <= utcNow)
            .ToArrayAsync(cancellationToken);
        if (expired.Length == 0) return;

        foreach (AccountHold hold in expired)
            hold.Release(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
