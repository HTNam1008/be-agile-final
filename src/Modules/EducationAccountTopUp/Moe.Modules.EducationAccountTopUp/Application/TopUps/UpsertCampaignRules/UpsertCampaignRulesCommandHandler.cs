using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

internal sealed class UpsertCampaignRulesCommandHandler(
    ITopUpCampaignRepository campaigns,
    IAdminAccessControl adminAccess) : ICommandHandler<UpsertCampaignRulesCommand>
{
    public async Task<Result> Handle(UpsertCampaignRulesCommand command, CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(new Error("NotFound", "Campaign not found."));

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
            return Result.Failure(TopUpErrors.OrganizationOutsideScope);

        if (!string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error("InvalidRecipientMode", "Rules can only be added to DYNAMIC_RULES campaigns."));

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Draft &&
            campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Paused)
        {
            return Result.Failure(new Error("InvalidStatus", "Rules can only be modified for DRAFT or PAUSED campaigns."));
        }

        var existingRules = await campaigns.GetRulesAsync(campaign.Id, cancellationToken);

        await campaigns.RemoveRulesAsync(existingRules, cancellationToken);

        foreach (var ruleDto in command.Rules)
        {
            var rule = TopUpCampaignRule.Create(
                topUpCampaignId: campaign.Id,
                criterionCode: ruleDto.CriterionCode.ToUpperInvariant(),
                operatorCode: ruleDto.OperatorCode.ToUpperInvariant(),
                numericValueFrom: ruleDto.NumericValueFrom,
                numericValueTo: ruleDto.NumericValueTo,
                textValue: ruleDto.TextValue
            );

            await campaigns.AddRuleAsync(rule, cancellationToken);
        }

        return Result.Success();
    }
}
