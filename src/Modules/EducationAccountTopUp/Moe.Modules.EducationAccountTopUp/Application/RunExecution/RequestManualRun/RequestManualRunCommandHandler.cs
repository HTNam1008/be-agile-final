using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed class RequestManualRunCommandHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpRunRepository runs,
    ITopUpRunDispatcher dispatcher,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>
{
    private const string TopUpsManagePermission = "TOPUPS_MANAGE";

    public async Task<Result<RequestManualRunResponse>> Handle(
        RequestManualRunCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentUser.HasPermission(TopUpsManagePermission))
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.Unauthorized);
        }

        TopUpCampaign? campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignNotFound);
        }

        if (!campaign.IsExecutable)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignNotExecutable);
        }

        TopUpRun? existingRun = await runs.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existingRun is not null)
        {
            return Result<RequestManualRunResponse>.Success(Map(existingRun));
        }

        if (currentUser.UserAccountId is not long requestedByUserId)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.ActorRequired);
        }

        DateTime requestedAtUtc = clock.UtcNow.UtcDateTime;
        TopUpRun run = TopUpRun.CreateManual(
            campaign,
            command.IdempotencyKey,
            requestedByUserId,
            requestedAtUtc,
            command.Note);

        await runs.AddAsync(run, cancellationToken);

        await dispatcher.EnqueueAsync(run.Id, cancellationToken);
        run.MarkManualRunRequested(requestedAtUtc);

        return Result<RequestManualRunResponse>.Success(Map(run));
    }

    private static RequestManualRunResponse Map(TopUpRun run)
    {
        return new RequestManualRunResponse
        {
            RunId = run.Id,
            Status = run.RunStatusCode,
            IdempotencyKey = run.IdempotencyKey,
            RequestedAtUtc = run.ScheduledForUtc
        };
    }
}
