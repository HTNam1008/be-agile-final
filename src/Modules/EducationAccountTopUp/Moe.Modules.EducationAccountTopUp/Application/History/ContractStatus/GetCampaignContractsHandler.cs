using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.ContractStatus;

internal sealed class GetCampaignContractsHandler(
    IContractStatusReader contractReader)
    : IQueryHandler<GetCampaignContractsQuery, PageResponse<CampaignContractItem>>
{
    public async Task<Result<PageResponse<CampaignContractItem>>> Handle(
        GetCampaignContractsQuery query,
        CancellationToken cancellationToken)
    {
        ContractStatusPage page = await contractReader.GetContractsAsync(
            query.CampaignId,
            query.ContractStatus,
            query.EducationAccountId,
            query.Page,
            query.PageSize,
            cancellationToken);

        CampaignContractItem[] items = page.Items
            .Select(x => new CampaignContractItem(
                x.ContractId,
                x.EducationAccountId,
                x.MaskedAccountNumber,
                x.StudentDisplayName,
                x.DeliveryTypeCode,
                x.AmountPerPayment,
                x.MaxTotalAmount,
                x.TotalReceived,
                x.CyclesCompleted,
                x.NextPaymentDate,
                x.ContractStatus))
            .ToArray();

        return Result<PageResponse<CampaignContractItem>>.Success(
            new PageResponse<CampaignContractItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }
}
