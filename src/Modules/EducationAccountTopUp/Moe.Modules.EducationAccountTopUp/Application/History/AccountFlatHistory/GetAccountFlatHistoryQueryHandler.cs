using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AccountFlatHistory;

internal sealed class GetAccountFlatHistoryQueryHandler(
    IEducationAccountRepository educationAccounts,
    IDynamicTopUpContractRepository contracts,
    ITopUpTransactionRepository transactions,
    ITopUpRunRepository runs,
    IPersonDirectory people,
    IAdminAccessControl adminAccess)
    : IQueryHandler<GetAccountFlatHistoryQuery, AccountFlatHistoryResponse>
{
    public async Task<Result<AccountFlatHistoryResponse>> Handle(
        GetAccountFlatHistoryQuery query,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByIdAsync(
            query.EducationAccountId,
            cancellationToken);

        if (account is null)
        {
            return Result<AccountFlatHistoryResponse>.Failure(EducationAccountErrors.NotFound);
        }

        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        if (person is null)
        {
            return Result<AccountFlatHistoryResponse>.Failure(AccountErrors.InvalidPerson);
        }

        if (person.OrganizationId is long organizationId)
        {
            Result access = adminAccess.EnsureCanAccessOrganization(organizationId);
            if (access.IsFailure)
            {
                return Result<AccountFlatHistoryResponse>.Failure(access.Error);
            }
        }
        else if (!adminAccess.IsHqAdmin)
        {
            return Result<AccountFlatHistoryResponse>.Failure(AccountErrors.OrganizationOutsideScope);
        }

        IReadOnlyList<DynamicTopUpContract> accountContracts =
            await contracts.GetByAccountIdAsync(query.EducationAccountId, cancellationToken);

        var contractByCampaign = accountContracts.ToDictionary(
            c => c.TopUpCampaignId,
            c => c);

        var contractSummaries = accountContracts
            .Select(c => new ContractSummary(
                c.TopUpCampaignId,
                c.DeliveryTypeCode,
                c.AmountPerPayment,
                c.MaxTotalAmount,
                c.TotalReceived,
                c.CyclesCompleted,
                c.ContractStatus,
                c.NextPaymentDate,
                c.DerivedTotalCycles))
            .ToArray();

        List<TopUpTransaction> allTransactions = await transactions.GetByAccountIdAsync(
            query.EducationAccountId,
            cancellationToken);

        var runIds = allTransactions.Select(t => t.TopUpRunId).Distinct().ToArray();
        var runsById = new Dictionary<long, TopUpRun>();
        foreach (var runId in runIds)
        {
            var run = await runs.GetByIdAsync(runId, cancellationToken);
            if (run is not null)
            {
                runsById[runId] = run;
            }
        }

        var totalCount = allTransactions.Count;
        var pagedTransactions = allTransactions
            .OrderByDescending(t => t.CompletedAtUtc ?? t.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        var items = pagedTransactions
            .Select(t =>
            {
                runsById.TryGetValue(t.TopUpRunId, out TopUpRun? run);
                long campaignId = run?.TopUpCampaignId ?? 0;
                contractByCampaign.TryGetValue(campaignId, out DynamicTopUpContract? contract);

                return new AccountFlatHistoryItem(
                    t.Id,
                    t.TopUpRunId,
                    campaignId,
                    t.Amount,
                    t.TransactionStatusCode,
                    t.Reason,
                    t.CreatedAtUtc,
                    t.CompletedAtUtc,
                    contract?.DeliveryTypeCode,
                    contract?.TotalReceived,
                    contract?.MaxTotalAmount,
                    contract?.ContractStatus);
            })
            .ToArray();

        return Result<AccountFlatHistoryResponse>.Success(
            new AccountFlatHistoryResponse(
                items,
                query.Page,
                query.PageSize,
                totalCount,
                contractSummaries,
                account.AccountNumber,
                person.DisplayName,
                account.CachedBalance,
                account.StatusCode));
    }
}

public sealed record AccountFlatHistoryResponse(
    IReadOnlyList<AccountFlatHistoryItem> Items,
    int Page,
    int PageSize,
    long TotalCount,
    IReadOnlyList<ContractSummary> Contracts,
    string AccountNumber,
    string StudentName,
    decimal CurrentBalance,
    string AccountStatus);

public sealed record ContractSummary(
    long CampaignId,
    string DeliveryTypeCode,
    decimal AmountPerPayment,
    decimal MaxTotalAmount,
    decimal TotalReceived,
    int CyclesCompleted,
    string ContractStatus,
    DateTime? NextPaymentDate,
    int? DerivedTotalCycles);

public sealed record AccountFlatHistoryItem(
    long TransactionId,
    long RunId,
    long CampaignId,
    decimal Amount,
    string Status,
    string? Reason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ContractDeliveryType,
    decimal? ContractTotalReceived,
    decimal? ContractMaxTotalAmount,
    string? ContractStatus);
