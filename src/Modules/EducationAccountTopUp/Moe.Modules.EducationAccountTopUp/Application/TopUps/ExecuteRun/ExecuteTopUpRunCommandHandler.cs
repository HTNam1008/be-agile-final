using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;

internal sealed class ExecuteTopUpRunCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<ExecuteTopUpRunCommand, long>
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

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCode.Active.ToString())
            return Result<long>.Failure(new Error("InvalidStatus", "Only ACTIVE campaigns can be executed."));

        var nowUtc = clock.UtcNow.UtcDateTime;
        var idempotencyKey = $"TOPUP-RUN:{campaign.Id}:MANUAL:{nowUtc.Ticks}";

        // 1. Create TopUpRun
        var ruleSnapshot = "[]"; // Simplified for MVP
        var run = TopUpRun.Create(
            topUpCampaignId: campaign.Id,
            campaignVersion: campaign.CampaignVersion,
            scheduledForUtc: nowUtc,
            triggerTypeCode: TopUpTriggerTypeCode.Manual.ToString().ToUpperInvariant(),
            triggeredByLoginAccountId: currentUser.UserAccountId,
            ruleSnapshotJson: ruleSnapshot,
            idempotencyKey: idempotencyKey,
            nowUtc: nowUtc);

        dbContext.Set<TopUpRun>().Add(run);

        // We could save early to reserve the idempotency key, but EF Core transaction is fine for now.

        // 2. Fetch Recipients
        var recipientsQuery = dbContext.Set<TopUpCampaignRecipient>()
            .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive);
        
        var accountsQuery = dbContext.Set<Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount>().AsQueryable();

        var matches = new List<(Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount Account, decimal Amount)>();

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
            // Dynamic Rules: For MVP, assume it selects all Active accounts (if no rules matched, we guard against zero rules anyway).
            // A real IQueryable evaluator goes here.
            var rules = await dbContext.Set<TopUpCampaignRule>()
                .Where(x => x.TopUpCampaignId == campaign.Id && x.IsActive)
                .ToListAsync(cancellationToken);
            
            if (rules.Count == 0)
                return Result<long>.Failure(new Error("ZeroRules", "Cannot execute DYNAMIC_RULES campaign without rules."));

            var allAccounts = await accountsQuery.ToListAsync(cancellationToken);
            foreach (var acc in allAccounts)
            {
                // Stub Rule Evaluator -> In real implementation, translates criteria to expression tree
                matches.Add((acc, campaign.DefaultTopUpAmount));
            }
        }

        int succeeded = 0, failed = 0, skipped = 0;
        decimal totalAmount = 0;

        foreach (var match in matches)
        {
            var acc = match.Account;
            var amount = match.Amount;
            var txIdempotency = $"TOPUP:{run.Id}:{acc.Id}";

            var topupTx = TopUpTransaction.Create(
                topUpRunId: run.Id,
                educationAccountId: acc.Id,
                topUpAmount: amount,
                reason: campaign.Reason,
                idempotencyKey: txIdempotency);

            if (acc.StatusCode != Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.AccountStatuses.Active)
            {
                topupTx.MarkSkipped("Account not ACTIVE", currentUser.UserAccountId ?? 0, nowUtc);
                skipped++;
                dbContext.Set<TopUpTransaction>().Add(topupTx);
                continue;
            }

            // Ledger Transaction
            var accTx = Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.AccountTransaction.Create(
                educationAccountId: acc.Id,
                transactionTypeCode: "CREDIT",
                amount: amount,
                referenceTypeCode: "TOPUP",
                referenceId: run.Id,
                idempotencyKey: txIdempotency,
                currentBalance: acc.CachedBalance,
                description: campaign.Reason,
                createdByUserId: currentUser.UserAccountId,
                nowUtc: nowUtc);

            dbContext.Set<Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.AccountTransaction>().Add(accTx);
            
            acc.UpdateBalance(amount);
            
            // Mark TopUpTx Complete (we don't have accTx.Id yet until DB save, so we'll just set it to 0 or we need to save changes first)
            topupTx.MarkCompleted(0, currentUser.UserAccountId ?? 0, nowUtc);
            
            dbContext.Set<TopUpTransaction>().Add(topupTx);
            succeeded++;
            totalAmount += amount;
        }

        run.UpdateProgress(succeeded, failed, totalAmount, nowUtc);
        
        string finalStatus = TopUpRunStatusCode.Completed.ToString().ToUpperInvariant();
        if (succeeded == 0) finalStatus = TopUpRunStatusCode.Failed.ToString().ToUpperInvariant();
        else if (skipped > 0 || failed > 0) finalStatus = TopUpRunStatusCode.Partial.ToString().ToUpperInvariant();

        run.Complete(finalStatus, nowUtc, matches.Count);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(run.Id);
    }
}
