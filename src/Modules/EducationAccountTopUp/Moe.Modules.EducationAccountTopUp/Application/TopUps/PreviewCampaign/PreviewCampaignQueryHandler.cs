using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

/// <summary>
/// Handles preview queries by delegating ALL persistence work to IGateway abstractions.
/// Zero EF Core, zero MoeDbContext. Clean Architecture enforced.
/// </summary>
internal sealed class PreviewCampaignQueryHandler(
    ITopUpCampaignReader campaignReader,
    IDynamicRuleFilter dynamicRuleFilter,
    IAdminAccessControl adminAccess,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students,
    IClock clock) : IQueryHandler<PreviewCampaignQuery, PreviewCampaignResult>
{
    public async Task<Result<PreviewCampaignResult>> Handle(
        PreviewCampaignQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load lightweight campaign summary — pure interface call, no EF Core
        CampaignPreviewSummary? campaign = await campaignReader.GetPreviewSummaryAsync(
            query.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result<PreviewCampaignResult>.Failure(TopUpErrors.CampaignNotFound);

        // 2. Access control
        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
            return Result<PreviewCampaignResult>.Failure(TopUpErrors.OrganizationOutsideScope);

        int skip = (query.PageNumber - 1) * query.PageSize;
        int take = query.PageSize;

        int totalMatched;
        decimal estimatedTotalAmount;
        List<PreviewAccountDto> samples;

        if (IsFixedSelection(campaign.RecipientModeCode))
        {
            // 3a. Fixed selection — all DB work behind ITopUpCampaignReader
            (int count, IReadOnlyList<PreviewFixedRecipient> items) =
                await campaignReader.GetFixedRecipientsForPreviewAsync(
                    campaign.Id, skip, take, cancellationToken);

            totalMatched = count;
            estimatedTotalAmount = items.Sum(x => x.EstimatedAmount);

            samples = items
                .Select(r => CreatePreviewSample(r.EducationAccountId, r.EstimatedAmount))
                .ToList();
        }
        else
        {
            // 3b. Dynamic rules — load rule projections via reader, filter via IDynamicRuleFilter
            IReadOnlyList<CampaignRuleGroupProjection> rules =
                await campaignReader.GetRulesAsync(campaign.Id, cancellationToken);

            if (rules.Count == 0)
                return Result<PreviewCampaignResult>.Failure(TopUpErrors.PreviewNoRules);

            DateTime nowUtc = clock.UtcNow.UtcDateTime;

            totalMatched = await dynamicRuleFilter.CountMatchingAccountsAsync(rules, nowUtc, cancellationToken);
            estimatedTotalAmount = totalMatched * campaign.DefaultTopUpAmount;

            IReadOnlyList<long> pagedIds = await dynamicRuleFilter.FilterAccountIdsAsync(
                rules, skip, take, nowUtc, cancellationToken);

            samples = pagedIds
                .Select(id => CreatePreviewSample(id, campaign.DefaultTopUpAmount))
                .ToList();
        }

        // 4. Enrich with student display info — all via IGateway
        List<PreviewAccountDto> enrichedSamples = await EnrichSamplesAsync(
            samples, campaign.OrganizationId, cancellationToken);

        return Result<PreviewCampaignResult>.Success(
            new PreviewCampaignResult(totalMatched, estimatedTotalAmount, enrichedSamples));
    }

    private static bool IsFixedSelection(string recipientModeCode)
        => string.Equals(recipientModeCode, "FixedSelection", StringComparison.OrdinalIgnoreCase)
        || string.Equals(recipientModeCode, "FIXED_SELECTION", StringComparison.OrdinalIgnoreCase);

    private static PreviewAccountDto CreatePreviewSample(long educationAccountId, decimal estimatedAmount)
        => new(educationAccountId,
            MaskedAccountNumber: "****",
            MaskedStudentNumber: null,
            StudentDisplayName: "Unavailable",
            estimatedAmount);

    private async Task<List<PreviewAccountDto>> EnrichSamplesAsync(
        IReadOnlyCollection<PreviewAccountDto> samples,
        long organizationId,
        CancellationToken cancellationToken)
    {
        long[] educationAccountIds = samples
            .Select(x => x.EducationAccountId)
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<long, TopUpAccountProjection> accountById =
            await accounts.FindByEducationAccountIdsAsync(educationAccountIds, cancellationToken);

        long[] personIds = accountById.Values
            .Select(x => x.PersonId)
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<long, TopUpStudentDisplaySummary> studentByPersonId =
            await students.FindDisplayByPersonIdsForTopUpAsync(personIds, organizationId, cancellationToken);

        return samples
            .Select(sample =>
            {
                accountById.TryGetValue(sample.EducationAccountId, out TopUpAccountProjection? account);

                TopUpStudentDisplaySummary? student = null;
                if (account is not null)
                    studentByPersonId.TryGetValue(account.PersonId, out student);

                return sample with
                {
                    MaskedAccountNumber = account is null
                        ? "****"
                        : TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber),
                    MaskedStudentNumber = student is null
                        ? null
                        : TopUpDisplayMasker.MaskStudentNumber(student.StudentNumber),
                    StudentDisplayName = student?.DisplayName ?? "Unavailable"
                };
            })
            .ToList();
    }
}
