using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.Filters;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;

public sealed record UpsertFixedRecipientDto(
    long EducationAccountId,
    decimal? AmountOverride);

public sealed record UpsertFixedRecipientsCommand(
    long TopUpCampaignId,
    TopUpAccountSelectionMode Mode,
    TopUpAccountFilter? Filter,
    IReadOnlyCollection<UpsertFixedRecipientDto> Recipients,
    IReadOnlyCollection<long> ExcludedEducationAccountIds)
    : ICommand<UpsertFixedRecipientsResponse>;

public sealed record UpsertFixedRecipientsResponse(
    long CampaignId,
    TopUpAccountSelectionMode Mode,
    int TotalMatched,
    int TotalExcluded,
    int TotalSelected);
