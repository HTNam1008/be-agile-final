using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

internal sealed class UpsertCampaignRulesCommandHandler(
    ITopUpCampaignRepository campaigns,
    IUnitOfWork unitOfWork,
    IAdminAccessControl adminAccess,
    IAuditService audit) : ICommandHandler<UpsertCampaignRulesCommand>
{
    public async Task<Result> Handle(UpsertCampaignRulesCommand command, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(TopUpErrors.CampaignNotFound);

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
            return Result.Failure(TopUpErrors.OrganizationOutsideScope);

        if (!string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
            return Result.Failure(TopUpErrors.RulesOnlyForDynamic);

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Draft &&
            campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Paused)
        {
            return Result.Failure(TopUpErrors.InvalidCampaignStatus);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await campaigns.DeleteRuleGroupsByCampaignIdAsync(campaign.Id, cancellationToken);

        int totalCriteria = 0;
        DateTime utcNow = DateTime.UtcNow;

        for (int groupIndex = 0; groupIndex < command.Groups.Count; groupIndex++)
        {
            UpsertRuleGroupDto groupDto = command.Groups[groupIndex];
            var group = TopUpRuleGroup.Create(campaign.Id, groupIndex + 1, utcNow);

            for (int ruleIndex = 0; ruleIndex < groupDto.Criteria.Count; ruleIndex++)
            {
                UpsertCampaignRuleDto ruleDto = groupDto.Criteria[ruleIndex];
                group.AddRule(
                    displayOrder: ruleIndex + 1,
                    criterionCode: ruleDto.CriterionCode.ToUpperInvariant(),
                    operatorCode: ruleDto.OperatorCode.ToUpperInvariant(),
                    numericValueFrom: ruleDto.NumericValueFrom,
                    numericValueTo: ruleDto.NumericValueTo,
                    textValue: ruleDto.TextValue);
                totalCriteria++;
            }

            await campaigns.AddRuleGroupAsync(group, cancellationToken);
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                AuditActionCodes.TopUpRulesUpdated,
                "TopUpCampaign",
                campaign.Id,
                campaign.OrganizationId,
                new SchoolAuditDetails(
                    "Top-up rule edits",
                    EntityDisplayName: campaign.CampaignName,
                    RelatedIds: new Dictionary<string, long> { ["campaignId"] = campaign.Id },
                    Count: command.Groups.Count + totalCriteria)),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
