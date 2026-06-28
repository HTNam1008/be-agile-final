using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.CancelRun;

internal sealed class CancelTopUpRunCommandHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpRunRepository runs,
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
            // The run is currently executing. Tell the orchestrator to abort.
            // The orchestrator will gracefully finalize the run with Skipped status for remaining.
            bool isActive = orchestrator.CancelRun(request.RunId);
            if (!isActive)
            {
                // ZOMBIE RUN DETECTED! The run is Processing but the orchestrator is not tracking it (e.g., worker crashed).
                // Force cancel it in the database directly.
                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsFailure) return cancelResult;
                await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(run, campaigns, clock.UtcNow.UtcDateTime, cancellationToken);
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
            await CampaignLifecycleHelper.EvaluateCampaignAfterTerminalRunAsync(run, campaigns, clock.UtcNow.UtcDateTime, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            return Result.Failure(TopUpErrors.InvalidRunTransition);
        }

        return Result.Success();
    }
}
