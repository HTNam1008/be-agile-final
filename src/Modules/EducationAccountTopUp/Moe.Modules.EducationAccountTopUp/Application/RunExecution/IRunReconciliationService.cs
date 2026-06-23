using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public interface IRunReconciliationService
{
    Task<Result<ReconciliationResult>> ReconcileRunAsync(
        long topUpRunId,
        CancellationToken cancellationToken = default);
}
