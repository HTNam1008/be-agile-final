using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public interface IRunExecutionOrchestrator
{
    Task<Result<RunExecutionResult>> ExecuteRunAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> recipients,
        CancellationToken cancellationToken = default);

    void CancelRun(long topUpRunId);
}
