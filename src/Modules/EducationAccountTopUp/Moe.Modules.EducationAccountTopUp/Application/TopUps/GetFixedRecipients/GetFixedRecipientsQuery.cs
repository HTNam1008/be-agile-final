using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.GetFixedRecipients;

public sealed record GetFixedRecipientsQuery(long TopUpCampaignId) : IQuery<IReadOnlyList<FixedRecipientDto>>;

public sealed record FixedRecipientDto(
    long EducationAccountId,
    string DisplayName,
    string MaskedAccountNumber,
    string StudentNumber,
    int Age,
    decimal Balance,
    string? SchoolingStatusCode,
    decimal? AmountOverride);
