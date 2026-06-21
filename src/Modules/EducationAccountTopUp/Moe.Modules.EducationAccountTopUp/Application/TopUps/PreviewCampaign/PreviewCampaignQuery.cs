using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;

public sealed record PreviewCampaignResult(
    int TotalMatchedAccounts,
    decimal EstimatedTotalAmount,
    List<PreviewAccountDto> Samples);

public sealed record PreviewAccountDto(
    long EducationAccountId,
    string MaskedAccountNumber,
    string? MaskedStudentNumber,
    string StudentDisplayName,
    decimal EstimatedAmount);

public sealed record PreviewCampaignQuery(
    long TopUpCampaignId,
    int PageNumber,
    int PageSize) : IQuery<PreviewCampaignResult>;

/// <summary>Lightweight campaign projection for preview access checks. Infrastructure-read, Application-consumed.</summary>
public sealed record CampaignPreviewSummary(
    long Id,
    long OrganizationId,
    string RecipientModeCode,
    decimal DefaultTopUpAmount);

/// <summary>A fixed recipient row resolved for preview purposes.</summary>
public sealed record PreviewFixedRecipient(
    long EducationAccountId,
    decimal EstimatedAmount);
