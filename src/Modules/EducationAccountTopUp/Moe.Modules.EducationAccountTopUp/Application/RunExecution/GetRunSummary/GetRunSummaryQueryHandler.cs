using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;

public sealed class GetRunSummaryQueryHandler(
    ITopUpRunRepository runs) : IQueryHandler<GetRunSummaryQuery, RunSummaryResponse>
{
    public async Task<Result<RunSummaryResponse>> Handle(
        GetRunSummaryQuery query,
        CancellationToken cancellationToken)
    {
        TopUpRun? run = await runs.GetByIdAsync(query.TopUpRunId, cancellationToken);
        if (run is null)
        {
            return Result<RunSummaryResponse>.Failure(TopUpErrors.RunNotFound);
        }

        return Result<RunSummaryResponse>.Success(new RunSummaryResponse
        {
            TopUpRunId = run.Id,
            CampaignId = run.TopUpCampaignId,
            RunStatus = run.RunStatusCode,
            TriggerType = run.TriggerTypeCode,
            TotalSelected = run.TotalSelected,
            TotalProcessed = run.TotalProcessed,
            TotalSucceeded = run.TotalSucceeded,
            TotalFailed = run.TotalFailed,
            TotalSkipped = run.TotalSkipped,
            TotalAmount = run.TotalAmount,
            RequestedAtUtc = run.ScheduledForUtc,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            Note = run.Note
        });
    }
}
