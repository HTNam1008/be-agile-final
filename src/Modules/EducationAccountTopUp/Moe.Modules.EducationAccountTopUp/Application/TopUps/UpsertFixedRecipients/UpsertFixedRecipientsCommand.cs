using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;

public sealed record UpsertFixedRecipientDto(
    long EducationAccountId,
    decimal? AmountOverride);

public sealed record UpsertFixedRecipientsCommand(
    long TopUpCampaignId,
    List<UpsertFixedRecipientDto> Recipients) : ICommand;
