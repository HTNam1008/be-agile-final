using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

public sealed record PreviewCampaignResult(
    int TotalMatchedAccounts,
    decimal EstimatedTotalAmount,
    List<PreviewAccountDto> Samples);

public sealed record PreviewAccountDto(
    long EducationAccountId,
    decimal EstimatedAmount);

public sealed record PreviewCampaignQuery(
    long TopUpCampaignId,
    int PageNumber,
    int PageSize) : IQuery<PreviewCampaignResult>;
