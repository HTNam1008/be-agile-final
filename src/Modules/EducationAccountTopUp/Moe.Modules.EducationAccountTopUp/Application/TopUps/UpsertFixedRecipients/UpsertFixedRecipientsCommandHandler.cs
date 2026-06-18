using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;

internal sealed class UpsertFixedRecipientsCommandHandler(
    MoeDbContext dbContext,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<UpsertFixedRecipientsCommand>
{
    public async Task<Result> Handle(UpsertFixedRecipientsCommand command, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Set<TopUpCampaign>()
            .FirstOrDefaultAsync(x => x.Id == command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
            return Result.Failure(new Error("NotFound", "Campaign not found."));

        // Cross-Cutting Auth Scope Check
        if (!currentUser.OrganizationUnitIds.Contains(campaign.OrganizationId) && currentUser.OrganizationUnitId != campaign.OrganizationId)
            return Result.Failure(new Error("Forbidden", "User does not have access to the requested OrganizationId."));

        if (!string.Equals(campaign.RecipientModeCode, RecipientModeCode.FixedSelection.ToString(), StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error("InvalidRecipientMode", "Recipients can only be added directly to FIXED_SELECTION campaigns."));

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCode.Draft.ToString() &&
            campaign.CampaignStatusCode != TopUpCampaignStatusCode.Paused.ToString())
        {
            return Result.Failure(new Error("InvalidStatus", "Recipients can only be modified for DRAFT or PAUSED campaigns."));
        }

        // Idempotent upsert: fetch existing and merge, or flush and replace for MVP.
        // For absolute correctness based on standard idempotent lists, we can delete non-matches, insert new, update existing.
        // Given L-006 is "Idempotent upsert", we do exactly that.

        var existingRecipients = await dbContext.Set<TopUpCampaignRecipient>()
            .Where(x => x.TopUpCampaignId == campaign.Id)
            .ToDictionaryAsync(x => x.EducationAccountId, cancellationToken);

        var newRecipientsList = command.Recipients.DistinctBy(x => x.EducationAccountId).ToList();
        var incomingIds = newRecipientsList.Select(x => x.EducationAccountId).ToHashSet();

        var nowUtc = clock.UtcNow.UtcDateTime;
        var userId = currentUser.UserAccountId ?? 0;

        // Delete those not in incoming list
        var toDelete = existingRecipients.Values.Where(x => !incomingIds.Contains(x.EducationAccountId)).ToList();
        dbContext.Set<TopUpCampaignRecipient>().RemoveRange(toDelete);

        // Update existing / Insert new
        foreach (var dto in newRecipientsList)
        {
            if (existingRecipients.TryGetValue(dto.EducationAccountId, out var existing))
            {
                // Update properties (we could add an Update method to TopUpCampaignRecipient, but since only AmountOverride can change here for an active record)
                // For a proper DDD approach, let's just delete and reinsert if we cannot mutate easily, or just update via reflection/EF tracking.
                // EF core will track it, but the property setter is private. 
                // Let's remove and re-add for absolute clean state, or use a method.
                // For now, removing and re-adding is safer if there's no Update method.
                dbContext.Set<TopUpCampaignRecipient>().Remove(existing);
                var newRec = TopUpCampaignRecipient.Create(campaign.Id, dto.EducationAccountId, dto.AmountOverride, userId, nowUtc);
                dbContext.Set<TopUpCampaignRecipient>().Add(newRec);
            }
            else
            {
                var newRec = TopUpCampaignRecipient.Create(campaign.Id, dto.EducationAccountId, dto.AmountOverride, userId, nowUtc);
                dbContext.Set<TopUpCampaignRecipient>().Add(newRec);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
