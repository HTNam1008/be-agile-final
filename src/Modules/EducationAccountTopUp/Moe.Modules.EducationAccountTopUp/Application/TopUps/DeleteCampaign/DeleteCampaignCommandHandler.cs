using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.DeleteCampaign;

internal sealed class DeleteCampaignCommandHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpCampaignDeletionRepository campaignDeletion,
    ITopUpRunRepository runs,
    IAdminAccessControl adminAccess,
    IAuditService audit,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteCampaignCommand>
{
    public async Task<Result> Handle(DeleteCampaignCommand command, CancellationToken cancellationToken)
    {
        TopUpCampaign? campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);
        if (campaign is null)
        {
            return Result.Failure(TopUpErrors.CampaignNotFound);
        }

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
        {
            return Result.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Draft)
        {
            return Result.Failure(TopUpErrors.CannotDeleteNonDraftCampaign);
        }

        if (await runs.HasRunsForCampaignAsync(campaign.Id, cancellationToken))
        {
            return Result.Failure(TopUpErrors.CannotDeleteCampaignWithRuns);
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.TopUpCampaignStatusChanged,
                "TopUpCampaign",
                campaign.Id,
                campaign.OrganizationId,
                new SchoolAuditDetails(
                    "Draft top-up campaign removed",
                    EntityDisplayName: campaign.CampaignName,
                    StatusTransition: new SchoolAuditStatusTransition(campaign.CampaignStatusCode, "REMOVED"))),
            cancellationToken);

        await campaignDeletion.DeleteDraftAsync(campaign, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
