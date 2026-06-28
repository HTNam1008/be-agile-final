using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed class RequestManualRunCommandHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpRunRepository runs,
    IUnitOfWork unitOfWork,
    ITopUpRunDispatcher dispatcher,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock) : ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>
{
    public async Task<Result<RequestManualRunResponse>> Handle(
        RequestManualRunCommand command,
        CancellationToken cancellationToken)
    {
        TopUpCampaign? campaign = await campaigns.GetByIdAsync(command.CampaignId, cancellationToken);
        if (campaign is null)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignNotFound);
        }

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
        {
            return Result<RequestManualRunResponse>.Failure(access.Error);
        }

        if (!campaign.IsExecutable)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignNotExecutable);
        }

        if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.ManualRunDisabled);
        }

        if (!campaign.ScheduleTypeCode.Equals("IMMEDIATE", StringComparison.OrdinalIgnoreCase))
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.ManualRunDisabled);
        }

        TopUpRun? existingRun = await runs.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existingRun is not null)
        {
            return Result<RequestManualRunResponse>.Success(Map(existingRun));
        }

        bool hasExistingRuns = await runs.HasRunsForCampaignAsync(campaign.Id, cancellationToken);
        if (hasExistingRuns)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignAlreadyExecuted);
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
        run.MarkManualRunRequested(requestedAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dispatcher.EnqueueAsync(run.Id, cancellationToken);

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
