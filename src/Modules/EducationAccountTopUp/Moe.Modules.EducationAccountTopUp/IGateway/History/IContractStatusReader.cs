using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.History;

internal interface IContractStatusReader
{
    Task<ContractStatusPage> GetContractsAsync(
        long campaignId,
        string? contractStatus,
        long? educationAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

internal sealed record ContractStatusPage(
    IReadOnlyList<ContractStatusProjection> Items,
    long TotalCount);

internal sealed record ContractStatusProjection(
    long ContractId,
    long EducationAccountId,
    string DeliveryTypeCode,
    decimal AmountPerPayment,
    decimal? MaxTotalAmount,
    decimal TotalReceived,
    int? CyclesCompleted,
    DateTime? NextPaymentDate,
    string ContractStatus,
    DateTime CreatedAtUtc,
    string? MaskedAccountNumber,
    string? StudentDisplayName);
