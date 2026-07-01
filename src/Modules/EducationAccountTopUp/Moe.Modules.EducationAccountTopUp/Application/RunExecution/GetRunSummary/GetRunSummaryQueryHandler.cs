using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;

internal sealed class GetRunSummaryQueryHandler(
    ITopUpRunSummaryReader reader,
    ITopUpAccessScopeResolver accessScopeResolver)
    : IQueryHandler<GetRunSummaryQuery, RunSummaryResponse>
{
    public async Task<Result<RunSummaryResponse>> Handle(
        GetRunSummaryQuery query,
        CancellationToken cancellationToken)
    {
        RunSummaryProjection? run = await reader.GetByIdAsync(
            query.TopUpRunId,
            cancellationToken);

        if (run is null
            || query.ExpectedCampaignId.HasValue
            && run.CampaignId != query.ExpectedCampaignId.Value)
        {
            return Result<RunSummaryResponse>.Failure(TopUpErrors.RunNotFound);
        }

        Result<TopUpAccessScope> accessResult =
            accessScopeResolver.Resolve(run.OrganizationId);

        if (accessResult.IsFailure)
        {
            return Result<RunSummaryResponse>.Failure(accessResult.Error);
        }

        return Result<RunSummaryResponse>.Success(new RunSummaryResponse
        {
            RunId = run.RunId,
            CampaignId = run.CampaignId,
            CampaignCode = run.CampaignCode,
            CampaignName = run.CampaignName,
            RunDateUtc = run.RunDateUtc,
            TriggerType = run.TriggerType,
            Status = run.Status,
            MatchedCount = run.MatchedCount,
            ProcessedCount = run.ProcessedCount,
            SucceededCount = run.SucceededCount,
            FailedCount = run.FailedCount,
            SkippedCount = run.SkippedCount,
            TotalCredited = run.TotalCredited,
            BudgetConsumedPercent = run.CampaignMaxTotalAmount > 0
                ? Math.Round((run.TotalCredited / run.CampaignMaxTotalAmount) * 100, 1)
                : null,
            DurationSeconds = run.StartedAtUtc is DateTime started && run.CompletedAtUtc is DateTime completed
                ? (int)(completed - started).TotalSeconds
                : null,
            StartedAtUtc = run.StartedAtUtc,
            CompletedAtUtc = run.CompletedAtUtc
        });
    }
}
