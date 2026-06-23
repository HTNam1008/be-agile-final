namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public sealed record EducationAccountPaymentBalance(
    long EducationAccountId,
    decimal CurrentBalance,
    decimal HeldBalance,
    decimal AvailableBalance,
    string CurrencyCode);

public interface IEducationAccountPaymentGateway
{
    Task<EducationAccountPaymentBalance?> GetAvailableBalanceAsync(
        long personId,
        CancellationToken cancellationToken);

    Task<long> ReserveAsync(
        long personId,
        long paymentPartId,
        decimal amount,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken);

    Task CaptureAsync(
        long accountHoldId,
        long? actorLoginAccountId,
        CancellationToken cancellationToken);

    Task ReleaseAsync(long accountHoldId, CancellationToken cancellationToken);

    Task<long> DebitImmediatelyAsync(
        long personId,
        long paymentPartId,
        decimal amount,
        long? actorLoginAccountId,
        CancellationToken cancellationToken);
}
