using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.History;

internal sealed class ContractStatusReader(MoeDbContext dbContext) : IContractStatusReader
{
    public async Task<ContractStatusPage> GetContractsAsync(
        long campaignId,
        string? contractStatus,
        long? educationAccountId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<DynamicTopUpContract> query = dbContext.Set<DynamicTopUpContract>()
            .AsNoTracking()
            .Where(c => c.TopUpCampaignId == campaignId);

        if (!string.IsNullOrWhiteSpace(contractStatus))
        {
            string status = contractStatus.Trim().ToUpperInvariant();
            query = query.Where(c => c.ContractStatus == status);
        }

        if (educationAccountId.HasValue)
        {
            query = query.Where(c => c.EducationAccountId == educationAccountId.Value);
        }

        long totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip(checked((page - 1) * pageSize))
            .Take(pageSize)
            .Select(c => new ContractStatusProjection(
                c.Id,
                c.EducationAccountId,
                c.DeliveryTypeCode,
                c.AmountPerPayment,
                c.MaxTotalAmount,
                c.TotalReceived,
                c.DerivedTotalCycles,
                c.NextPaymentDate,
                c.ContractStatus,
                null,
                null))
            .ToArrayAsync(cancellationToken);

        if (items.Length == 0)
        {
            return new ContractStatusPage(items, totalCount);
        }

        long[] accountIds = items.Select(x => x.EducationAccountId).Distinct().ToArray();
        var accounts = await dbContext.Set<EducationAccount>()
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a, cancellationToken);

        var result = items.Select(item =>
        {
            accounts.TryGetValue(item.EducationAccountId, out var account);
            string? masked = account is not null
                ? Application.TopUps.TopUpDisplayMasker.MaskAccountNumber(account.AccountNumber)
                : null;
            return item with
            {
                MaskedAccountNumber = masked,
                StudentDisplayName = null
            };
        }).ToArray();

        return new ContractStatusPage(result, totalCount);
    }
}
