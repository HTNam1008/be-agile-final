namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public sealed class EducationAccountPaymentUnavailableException(string message) : InvalidOperationException(message);

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

    Task<long> CaptureAsync(
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

    Task<long> CreditRefundAsync(
        long personId,
        long refundReferenceId,
        decimal amount,
        long? reversalOfTransactionId,
        string idempotencyKey,
        long? actorLoginAccountId,
        CancellationToken cancellationToken);
}
