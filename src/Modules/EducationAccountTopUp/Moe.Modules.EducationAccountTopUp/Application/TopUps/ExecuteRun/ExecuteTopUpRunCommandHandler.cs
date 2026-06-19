using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;

internal sealed class ExecuteTopUpRunCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock,
    RecipientProcessingService recipientProcessingService) : ICommandHandler<ExecuteTopUpRunCommand, long>
{
    public async Task<Result<long>> Handle(ExecuteTopUpRunCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .FirstOrDefaultAsync(x => x.Id == command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result<long>.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result<long>.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Active)
            return Result<long>.Failure(new Error("InvalidStatus", "Only ACTIVE campaigns can be executed."));

        var nowUtc = clock.UtcNow.UtcDateTime;
        var idempotencyKey = $"TOPUP-RUN:{campaign.Id}:MANUAL:{nowUtc.Ticks}";

        // 1. Create TopUpRun
        var run = TopUpRun.CreateManual(
            campaign,
            idempotencyKey,
            currentUser.UserAccountId ?? 0,
            nowUtc,
            note: null);

        run.StartProcessing(nowUtc);

        dbContext.Set<TopUpRun>().Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Fetch Recipients
        var recipientsQuery = dbContext.Set<TopUpCampaignRecipient>()
            .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive);
        
        var accountsQuery = dbContext.Set<EducationAccount>().AsQueryable();

        var matches = new List<(EducationAccount Account, decimal Amount)>();

        if (string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var fixedRecipients = await recipientsQuery.ToListAsync(cancellationToken);
            var accountIds = fixedRecipients.Select(x => x.EducationAccountId).ToList();
            
            var accounts = await accountsQuery
                .Where(x => accountIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var fr in fixedRecipients)
            {
                if (accounts.TryGetValue(fr.EducationAccountId, out var acc))
                {
                    matches.Add((acc, fr.AmountOverride ?? campaign.DefaultTopUpAmount));
                }
            }
        }
        else
        {
            var rules = await dbContext.Set<TopUpCampaignRule>()
                .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            
            if (rules.Count == 0)
                return Result<long>.Failure(new Error("ZeroRules", "Cannot execute DYNAMIC_RULES campaign without rules."));

            var activeAccountsQuery = accountsQuery
                .Where(x => x.StatusCode == AccountStatuses.Active);

            activeAccountsQuery = DynamicRuleEvaluator.ApplyRules(dbContext, activeAccountsQuery, rules, nowUtc);

            var accountIds = await activeAccountsQuery.Select(x => x.Id).ToListAsync(cancellationToken);
            foreach (var accId in accountIds)
            {
                var acc = await dbContext.Set<EducationAccount>().FindAsync(new object[] { accId }, cancellationToken);
                if (acc != null)
                {
                     matches.Add((acc, campaign.DefaultTopUpAmount));
                }
            }
        }

        int succeeded = 0, failed = 0, skipped = 0;
        decimal totalAmount = 0;

        foreach (var match in matches)
        {
            Result<RecipientProcessingResult> recipientResult;
            try
            {
                recipientResult = await recipientProcessingService.ProcessRecipientAsync(
                    run.Id,
                    match.Account.Id,
                    match.Amount,
                    campaign.OrganizationId,
                    campaign.Reason,
                    cancellationToken);
            }
            catch
            {
                failed++;
                continue;
            }

            if (recipientResult.IsFailure)
            {
                failed++;
                continue;
            }

            switch (recipientResult.Value.Status)
            {
                case TopUpTransactionStatusCodes.Completed:
                    succeeded++;
                    totalAmount += recipientResult.Value.CreditedAmount;
                    break;
                case TopUpTransactionStatusCodes.Skipped:
                    skipped++;
                    break;
                case TopUpTransactionStatusCodes.Failed:
                    failed++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        Result finalize = run.Finalize(matches.Count, succeeded, failed, skipped, totalAmount, nowUtc);
        if (finalize.IsFailure)
            return Result<long>.Failure(finalize.Error);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(run.Id);
    }
}
