using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed class TopUpTransaction : Entity<long>
{
    private TopUpTransaction() : base(0) { }

    private TopUpTransaction(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        DateTime createdAtUtc) : base(0)
    {
        TopUpRunId = topUpRunId;
        EducationAccountId = educationAccountId;
        IdempotencyKey = BuildIdempotencyKey(topUpRunId, educationAccountId);
        TransactionStatusCode = TopUpTransactionStatusCodes.Pending;
        Amount = amount;
        CreatedAtUtc = createdAtUtc;
    }

    public long TopUpRunId { get; private set; }
    public long EducationAccountId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string TransactionStatusCode { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public long? AccountTransactionId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private bool IsTerminal => TopUpTransactionStatusCodes.TerminalStatuses.Contains(TransactionStatusCode);

    public static TopUpTransaction Create(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        DateTime utcNow)
    {
        return new TopUpTransaction(topUpRunId, educationAccountId, amount, utcNow);
    }

    public Result Complete(long accountTransactionId, DateTime utcNow)
    {
        Result transition = EnsureCanTransition();
        if (transition.IsFailure)
        {
            return transition;
        }

        if (accountTransactionId <= 0)
        {
            return Result.Failure(TopUpErrors.InvalidAccountTransactionReference);
        }

        TransactionStatusCode = TopUpTransactionStatusCodes.Completed;
        AccountTransactionId = accountTransactionId;
        CompletedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Fail(string? reason, DateTime utcNow)
    {
        Result transition = EnsureCanTransition();
        if (transition.IsFailure)
        {
            return transition;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(TopUpErrors.TransactionReasonRequired);
        }

        TransactionStatusCode = TopUpTransactionStatusCodes.Failed;
        // Amount preserved — never zero the intended amount
        Reason = reason.Trim();
        CompletedAtUtc = utcNow;
        return Result.Success();
    }

    public Result Skip(string? reason, DateTime utcNow)
    {
        Result transition = EnsureCanTransition();
        if (transition.IsFailure)
        {
            return transition;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(TopUpErrors.TransactionReasonRequired);
        }

        TransactionStatusCode = TopUpTransactionStatusCodes.Skipped;
        // Amount preserved — never zero the intended amount
        Reason = reason.Trim();
        CompletedAtUtc = utcNow;
        return Result.Success();
    }

    private Result EnsureCanTransition()
    {
        Result terminal = EnsureNotTerminal();
        return terminal.IsFailure ? terminal : EnsurePending();
    }

    private Result EnsureNotTerminal()
    {
        return IsTerminal
            ? Result.Failure(TopUpErrors.TransactionIsTerminal)
            : Result.Success();
    }

    private Result EnsurePending()
    {
        return TransactionStatusCode == TopUpTransactionStatusCodes.Pending
            ? Result.Success()
            : Result.Failure(TopUpErrors.TransactionNotPending);
    }

    private static string BuildIdempotencyKey(long topUpRunId, long educationAccountId)
    {
        return $"topup:{topUpRunId}:{educationAccountId}";
    }
}
