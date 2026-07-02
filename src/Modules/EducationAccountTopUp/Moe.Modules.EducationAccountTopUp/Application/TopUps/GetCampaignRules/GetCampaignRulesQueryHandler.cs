using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;

internal sealed class GetCampaignRulesQueryHandler(
    ITopUpCampaignRepository campaigns,
    ITopUpCampaignReader reader,
    ICurrentUser currentUser)
    : IQueryHandler<GetCampaignRulesQuery, IReadOnlyList<CampaignRuleGroupDto>>
{
    public async Task<Result<IReadOnlyList<CampaignRuleGroupDto>>> Handle(
        GetCampaignRulesQuery query,
        CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(query.TopUpCampaignId, cancellationToken);

        if (campaign is null)
        {
            return Result<IReadOnlyList<CampaignRuleGroupDto>>.Failure(TopUpErrors.CampaignNotFound);
        }

        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId)
            && currentUser.OrganizationUnitId != campaign.OrganizationId)
        {
            return Result<IReadOnlyList<CampaignRuleGroupDto>>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        var rules = await reader.GetRulesAsync(query.TopUpCampaignId, cancellationToken);
        var ruleDtos = rules
            .Select(group => new CampaignRuleGroupDto(
                group.GroupId.ToString(),
                group.DisplayOrder,
                group.Criteria
                    .Select(x => new CampaignRuleDto(
                        x.Id.ToString(),
                        x.DisplayOrder,
                        x.CriterionCode,
                        x.OperatorCode,
                        x.NumericValueFrom,
                        x.NumericValueTo,
                        x.TextValue))
                    .ToList()))
            .ToList();

        return Result<IReadOnlyList<CampaignRuleGroupDto>>.Success(ruleDtos);
    }
}
