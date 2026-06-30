using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.History.ContractStatus;

public sealed record GetCampaignContractsQuery(
    long CampaignId,
    string? ContractStatus,
    long? EducationAccountId,
    int Page,
    int PageSize) : IQuery<PageResponse<CampaignContractItem>>;

public sealed record CampaignContractItem(
    long ContractId,
    long EducationAccountId,
    string? MaskedAccountNumber,
    string? StudentDisplayName,
    string DeliveryTypeCode,
    decimal AmountPerPayment,
    decimal? MaxTotalAmount,
    decimal TotalReceived,
    int? CyclesCompleted,
    DateTime? NextPaymentDate,
    string ContractStatus,
    DateTime CreatedAtUtc);
