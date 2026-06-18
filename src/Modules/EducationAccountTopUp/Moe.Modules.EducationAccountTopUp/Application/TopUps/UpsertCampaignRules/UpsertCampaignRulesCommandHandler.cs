using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;

internal sealed class UpsertCampaignRulesCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser) : ICommandHandler<UpsertCampaignRulesCommand>
{
    public async Task<Result> Handle(UpsertCampaignRulesCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .FirstOrDefaultAsync(x => x.Id == command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        if (!string.Equals(campaign.RecipientModeCode, RecipientModeCode.DynamicRules.ToString(), StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error("InvalidRecipientMode", "Rules can only be added to DYNAMIC_RULES campaigns."));

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCode.Draft.ToString() &&
            campaign.CampaignStatusCode != TopUpCampaignStatusCode.Paused.ToString())
        {
            return Result.Failure(new Error("InvalidStatus", "Rules can only be modified for DRAFT or PAUSED campaigns."));
        }

        // Flush existing rules (physical delete for MVP, or logical delete if required by audit)
        // Since MVP allows flushing, we will delete existing rules for this campaign.
        var existingRules = await dbContext.Set<TopUpCampaignRule>()
            .Where(x => x.TopUpCampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        dbContext.Set<TopUpCampaignRule>().RemoveRange(existingRules);

        // Insert new rules
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

            dbContext.Set<TopUpCampaignRule>().Add(rule);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
