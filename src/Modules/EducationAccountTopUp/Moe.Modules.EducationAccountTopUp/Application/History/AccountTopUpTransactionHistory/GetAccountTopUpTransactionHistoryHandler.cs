using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AccountTopUpTransactionHistory;

internal sealed class GetAccountTopUpTransactionHistoryHandler(
    ITopUpTransactionReader transactionReader)
    : IQueryHandler<GetAccountTopUpTransactionHistoryQuery, PageResponse<AccountTopUpTransactionHistoryItem>>
{
    public async Task<Result<PageResponse<AccountTopUpTransactionHistoryItem>>> Handle(
        GetAccountTopUpTransactionHistoryQuery query,
        CancellationToken cancellationToken)
    {
        TransactionHistoryPage page = await transactionReader.GetAccountTransactionsAsync(
            query.EducationAccountId,
            query.Filter,
            query.Page,
            query.PageSize,
            cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result<PageResponse<AccountTopUpTransactionHistoryItem>>.Success(
                new PageResponse<AccountTopUpTransactionHistoryItem>(
                    [],
                    query.Page,
                    query.PageSize,
                    page.TotalCount));
        }

        AccountTopUpTransactionHistoryItem[] items = page.Items
            .Select(item => new AccountTopUpTransactionHistoryItem(
                item.TransactionId,
                item.RunId,
                item.CampaignName,
                item.Amount,
                CurrencyCodes.SingaporeDollar,
                item.StatusCode,
                TopUpSafeReasonPresenter.Present(item.Reason),
                item.CompletedAtUtc,
                item.RunDateUtc))
            .ToArray();

        return Result<PageResponse<AccountTopUpTransactionHistoryItem>>.Success(
            new PageResponse<AccountTopUpTransactionHistoryItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }
}
