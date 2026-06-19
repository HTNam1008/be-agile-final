namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public interface IPendingTransactionRecoveryService
{
    Task<int> RecoverPendingTransactionsAsync(
        long topUpRunId,
        string campaignReason,
        CancellationToken cancellationToken = default);
}
