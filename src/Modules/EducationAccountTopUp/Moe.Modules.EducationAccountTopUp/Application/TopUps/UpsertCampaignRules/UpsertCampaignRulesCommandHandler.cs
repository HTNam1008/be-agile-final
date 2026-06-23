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
    IAdminAccessControl adminAccess) : ICommandHandler<UpsertCampaignRulesCommand>
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
