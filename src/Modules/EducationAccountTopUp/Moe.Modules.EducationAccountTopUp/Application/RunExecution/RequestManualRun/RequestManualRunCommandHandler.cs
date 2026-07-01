using Moe.Application.Abstractions.Audit;
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
    IClock clock,
    IAuditService audit) : ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>
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

        TopUpRun? existingRun = await runs.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (existingRun is not null)
        {
            return Result<RequestManualRunResponse>.Success(Map(existingRun));
        }

        bool hasActiveRuns = await runs.HasActiveRunsForCampaignAsync(campaign.Id, cancellationToken);
        if (hasActiveRuns)
        {
            return Result<RequestManualRunResponse>.Failure(TopUpErrors.ActiveRunInProgress);
        }

        if (IsImmediateCampaign(campaign))
        {
            bool hasExistingRuns = await runs.HasRunsForCampaignAsync(campaign.Id, cancellationToken);
            if (hasExistingRuns)
            {
                return Result<RequestManualRunResponse>.Failure(TopUpErrors.CampaignAlreadyExecuted);
            }
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

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.TopUpManualRunRequested,
                "TopUpRun",
                run.Id,
                campaign.OrganizationId,
                new SchoolAuditDetails(
                    "Manual top-up run requested",
                    EntityDisplayName: campaign.CampaignName,
                    RelatedIds: new Dictionary<string, long> { ["campaignId"] = campaign.Id })),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dispatcher.EnqueueAsync(run.Id, cancellationToken);

        Console.WriteLine($"[TopUp][RequestManualRun] Enqueued run {run.Id} for campaign {campaign.Id} by user {requestedByUserId}");

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

    private static bool IsImmediateCampaign(TopUpCampaign campaign)
    {
        return string.Equals(
            campaign.ScheduleTypeCode,
            ScheduleTypeCode.Immediate.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
