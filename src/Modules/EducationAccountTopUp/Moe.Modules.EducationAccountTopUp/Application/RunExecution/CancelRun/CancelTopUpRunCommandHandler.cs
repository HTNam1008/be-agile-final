using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.CancelRun;

internal sealed class CancelTopUpRunCommandHandler(
    ITopUpCampaignRepository campaigns,
    IDynamicTopUpContractRepository contracts,
    ITopUpRunRepository runs,
    ITopUpTransactionRepository transactions,
    IRunExecutionOrchestrator orchestrator,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<CancelTopUpRunCommand>
{
    public async Task<Result> Handle(CancelTopUpRunCommand request, CancellationToken cancellationToken)
    {
        TopUpRun? run = await runs.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return Result.Failure(TopUpErrors.RunNotFound);
        }

        if (TopUpRunStatusCodes.TerminalStatuses.Contains(run.RunStatusCode))
        {
            return Result.Failure(TopUpErrors.RunAlreadyTerminal);
        }

        if (run.RunStatusCode == TopUpRunStatusCodes.Processing)
        {
            // Persist cancellation intent so it survives pod restarts.
            run.RequestCancel(clock.UtcNow.UtcDateTime);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            // Also tell the in-memory orchestrator to abort immediately if it's tracking this run.
            bool isActive = orchestrator.CancelRun(request.RunId);
            if (!isActive)
            {
                // ZOMBIE RUN DETECTED! The run is Processing but the orchestrator is not tracking it (e.g., worker crashed).
                // Force cancel it in the database directly.
                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsFailure) return cancelResult;

                var allTransactions = await transactions.GetByRunIdAsync(request.RunId, cancellationToken);
                foreach (var tx in allTransactions.Where(t => t.TransactionStatusCode == TopUpTransactionStatusCodes.Pending))
                {
                    tx.Skip("Run cancelled manually during execution", clock.UtcNow.UtcDateTime);
                }

                await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(run, campaigns, contracts, clock.UtcNow.UtcDateTime, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        else if (run.RunStatusCode == TopUpRunStatusCodes.Previewed)
        {
            // The run hasn't started yet. Cancel it outright in the DB.
            Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
            if (cancelResult.IsFailure)
            {
                return cancelResult;
            }
            await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(run, campaigns, contracts, clock.UtcNow.UtcDateTime, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            return Result.Failure(TopUpErrors.InvalidRunTransition);
        }

        return Result.Success();
    }
}
