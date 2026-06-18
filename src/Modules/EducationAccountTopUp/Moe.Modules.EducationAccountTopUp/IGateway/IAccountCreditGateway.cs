using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface IAccountCreditGateway
{
    Task<Result<CreditAccountResult>> CreditAccountForTopUpAsync(
        long educationAccountId,
        decimal amount,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed record CreditAccountResult
{
    public required long AccountTransactionId { get; init; }
    public required bool AlreadyProcessed { get; init; }
}
