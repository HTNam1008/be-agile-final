using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;

internal sealed class GetCampaignRulesQueryHandler(
    ITopUpCampaignRepository campaigns,
    IAdminAccessControl adminAccess)
    : IQueryHandler<GetCampaignRulesQuery, IReadOnlyList<CampaignRuleDto>>
{
    public async Task<Result<IReadOnlyList<CampaignRuleDto>>> Handle(
        GetCampaignRulesQuery query,
        CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(query.TopUpCampaignId, cancellationToken);

        if (campaign is null)
        {
            return Result<IReadOnlyList<CampaignRuleDto>>.Failure(TopUpErrors.CampaignNotFound);
        }

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<CampaignRuleDto>>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        var rules = await campaigns.GetRulesAsync(query.TopUpCampaignId, cancellationToken);
        var ruleDtos = rules
            .Select(x => new CampaignRuleDto(
                x.Id.ToString(),
                x.CriterionCode,
                x.OperatorCode,
                x.NumericValueFrom,
                x.NumericValueTo,
                x.TextValue))
            .ToList();

        return Result<IReadOnlyList<CampaignRuleDto>>.Success(ruleDtos);
    }
}
