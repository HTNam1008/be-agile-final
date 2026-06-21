using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;

internal sealed class ExecuteTopUpRunCommandHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpRunRepository runs,
    ITopUpRunDispatcher dispatcher,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock) : ICommandHandler<ExecuteTopUpRunCommand, long>
{
    public async Task<Result<long>> Handle(ExecuteTopUpRunCommand command, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result<long>.Failure(new Error("NotFound", "Campaign not found."));

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
            return Result<long>.Failure(TopUpErrors.OrganizationOutsideScope);

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Active)
            return Result<long>.Failure(new Error("InvalidStatus", "Only ACTIVE campaigns can be executed."));

        var nowUtc = clock.UtcNow.UtcDateTime;
        var idempotencyKey = $"TOPUP-RUN:{campaign.Id}:MANUAL:{nowUtc.Ticks}";

        var run = TopUpRun.CreateManual(
            campaign,
            idempotencyKey,
            currentUser.UserAccountId ?? 0,
            nowUtc,
            note: null);

        await runs.AddAsync(run, cancellationToken);
        await dispatcher.EnqueueAsync(run.Id, cancellationToken);

        return Result<long>.Success(run.Id);
    }
}
