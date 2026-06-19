using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

internal sealed class PreviewCampaignQueryHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    ITopUpAccountProjectionRepository accounts,
    ITopUpStudentSearchDirectory students) : IQueryHandler<PreviewCampaignQuery, PreviewCampaignResult>
{
    public async Task<Result<PreviewCampaignResult>> Handle(PreviewCampaignQuery query, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result<PreviewCampaignResult>.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result<PreviewCampaignResult>.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        var accountsQuery = dbContext.Set<Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount>()
            .AsNoTracking();

        int totalMatched = 0;
        decimal estimatedTotalAmount = 0;
        var samples = new List<PreviewAccountDto>();

        if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var fixedRecipients = await dbContext.Set<TopUpCampaignRecipient>()
                .AsNoTracking()
                .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            
            var accountIds = fixedRecipients.Select(x => x.EducationAccountId).ToList();
            
            var activeAccounts = await accountsQuery
                .Where(x => accountIds.Contains(x.Id) && x.StatusCode == Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.AccountStatuses.Active)
                .Select(x => x.Id)
                .ToHashSetAsync(cancellationToken);

            foreach (var fr in fixedRecipients)
            {
                if (activeAccounts.Contains(fr.EducationAccountId))
                {
                    totalMatched++;
                    var amt = fr.AmountOverride ?? campaign.DefaultTopUpAmount;
                    estimatedTotalAmount += amt;
                    
                    var skip = (query.PageNumber - 1) * query.PageSize;
                    if (totalMatched > skip && samples.Count < query.PageSize)
                        samples.Add(CreatePreviewSample(fr.EducationAccountId, amt));
                }
            }
        }
        else
        {
            var rules = await dbContext.Set<TopUpCampaignRule>()
                .AsNoTracking()
                .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            
            if (rules.Count == 0)
                return Result<PreviewCampaignResult>.Failure(new Error("ZeroRules", "Cannot preview DYNAMIC_RULES campaign without rules."));

            // L-005: Dynamic Rule query specification builder
            var activeAccountsQuery = accountsQuery
                .Where(x => x.StatusCode == Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.AccountStatuses.Active);

            activeAccountsQuery = DynamicRuleEvaluator.ApplyRules(dbContext, activeAccountsQuery, rules, DateTime.UtcNow);

            var totalAccounts = await activeAccountsQuery.CountAsync(cancellationToken);
            totalMatched = totalAccounts;
            estimatedTotalAmount = totalAccounts * campaign.DefaultTopUpAmount;

            var skip = (query.PageNumber - 1) * query.PageSize;
            
            var pagedAccounts = await activeAccountsQuery
                .OrderBy(x => x.Id)
                .Skip(skip)
                .Take(query.PageSize)
                .Select(x => new { x.Id })
                .ToListAsync(cancellationToken);

            foreach (var acc in pagedAccounts)
            {
                samples.Add(CreatePreviewSample(acc.Id, campaign.DefaultTopUpAmount));
            }
        }

        List<PreviewAccountDto> enrichedSamples = await EnrichSamplesAsync(
            samples,
            campaign.OrganizationId,
            cancellationToken);

        return Result<PreviewCampaignResult>.Success(new PreviewCampaignResult(totalMatched, estimatedTotalAmount, enrichedSamples));
    }

    private static PreviewAccountDto CreatePreviewSample(long educationAccountId, decimal estimatedAmount)
        => new(
            educationAccountId,
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
            await accounts.FindByEducationAccountIdsAsync(
                educationAccountIds,
                cancellationToken);

        long[] personIds = accountById.Values
            .Select(x => x.PersonId)
            .Distinct()
            .ToArray();

        var studentByPersonId =
            await students.FindDisplayByPersonIdsForTopUpAsync(
                personIds,
                organizationId,
                cancellationToken);

        return samples
            .Select(sample =>
            {
                accountById.TryGetValue(sample.EducationAccountId, out TopUpAccountProjection? account);

                TopUpStudentDisplaySummary? student = null;
                if (account is not null)
                {
                    studentByPersonId.TryGetValue(account.PersonId, out student);
                }

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
