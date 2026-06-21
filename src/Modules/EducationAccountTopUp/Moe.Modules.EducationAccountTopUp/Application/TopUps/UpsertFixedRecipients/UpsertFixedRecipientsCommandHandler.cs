using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;

internal sealed class UpsertFixedRecipientsCommandHandler(
    ITopUpCampaignRepository campaigns,
    ICurrentUser currentUser,
    IAdminAccessControl adminAccess,
    IClock clock,
    ITopUpAccountSelectionResolver selectionResolver)
    : ICommandHandler<UpsertFixedRecipientsCommand, UpsertFixedRecipientsResponse>
{
    public async Task<Result<UpsertFixedRecipientsResponse>> Handle(
        UpsertFixedRecipientsCommand command,
        CancellationToken cancellationToken)
    {
        var campaign = await campaigns.GetByIdAsync(command.TopUpCampaignId, cancellationToken);

        if (campaign is null)
        {
            return Result<UpsertFixedRecipientsResponse>.Failure(TopUpErrors.CampaignNotFound);
        }

        Result access = adminAccess.EnsureCanAccessOrganization(campaign.OrganizationId);
        if (access.IsFailure)
        {
            return Result<UpsertFixedRecipientsResponse>.Failure(TopUpErrors.OrganizationOutsideScope);
        }

        if (!string.Equals(
                campaign.RecipientModeCode,
                RecipientModeCode.FixedSelection.ToString(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<UpsertFixedRecipientsResponse>.Failure(TopUpErrors.InvalidRecipientMode);
        }

        if (campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Draft
            && campaign.CampaignStatusCode != TopUpCampaignStatusCodes.Paused)
        {
            return Result<UpsertFixedRecipientsResponse>.Failure(TopUpErrors.InvalidCampaignStatus);
        }

        TopUpAccountSelection selection = command.Mode switch
        {
            TopUpAccountSelectionMode.ExplicitIds => TopUpAccountSelection.Explicit(
                command.Recipients.Select(x => x.EducationAccountId).ToArray()),
            TopUpAccountSelectionMode.AllMatchingFilter => TopUpAccountSelection.AllMatching(
                command.Filter!,
                command.ExcludedEducationAccountIds),
            _ => new TopUpAccountSelection(
                command.Mode,
                command.Filter,
                command.Recipients.Select(x => x.EducationAccountId).ToArray(),
                command.ExcludedEducationAccountIds)
        };

        Result<TopUpAccountSelectionResolution> selectionResult =
            await selectionResolver.ResolveAsync(selection, cancellationToken);

        if (selectionResult.IsFailure)
        {
            return Result<UpsertFixedRecipientsResponse>.Failure(selectionResult.Error);
        }

        var existingList = await campaigns.GetRecipientsAsync(campaign.Id, cancellationToken);
        var existingRecipients = existingList.ToDictionary(x => x.EducationAccountId);

        TopUpAccountSelectionResolution resolution = selectionResult.Value;
        HashSet<long> incomingIds = resolution.EducationAccountIds.ToHashSet();
        IReadOnlyDictionary<long, decimal?> amountOverrides = command.Mode == TopUpAccountSelectionMode.ExplicitIds
            ? command.Recipients.ToDictionary(x => x.EducationAccountId, x => x.AmountOverride)
            : new Dictionary<long, decimal?>();

        var nowUtc = clock.UtcNow.UtcDateTime;
        var userId = currentUser.UserAccountId ?? 0;

        var toDelete = existingRecipients.Values.Where(x => !incomingIds.Contains(x.EducationAccountId)).ToList();
        await campaigns.RemoveRecipientsAsync(toDelete, cancellationToken);

        foreach (long educationAccountId in resolution.EducationAccountIds)
        {
            decimal? amountOverride = amountOverrides.GetValueOrDefault(educationAccountId);

            if (existingRecipients.TryGetValue(educationAccountId, out var existing))
            {
                existing.UpdateAmountOverride(amountOverride);
            }
            else
            {
                var newRec = TopUpCampaignRecipient.Create(
                    campaign.Id,
                    educationAccountId,
                    amountOverride,
                    userId,
                    nowUtc);
                await campaigns.AddRecipientAsync(newRec, cancellationToken);
            }
        }

        return Result<UpsertFixedRecipientsResponse>.Success(
            new UpsertFixedRecipientsResponse(
                campaign.Id,
                command.Mode,
                resolution.TotalMatched,
                resolution.TotalExcluded,
                resolution.TotalSelected));
    }
}
