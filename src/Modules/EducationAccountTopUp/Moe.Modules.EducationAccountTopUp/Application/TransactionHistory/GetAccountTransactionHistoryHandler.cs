using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TransactionHistory;

internal sealed class GetAccountTransactionHistoryHandler(
    IEducationAccountRepository educationAccounts,
    IPersonDirectory people,
    IAdminAccessControl adminAccess,
    IAccountTransactionHistoryReader historyReader,
    ILoginAccountDisplayDirectory loginAccountDisplayDirectory)
    : IQueryHandler<GetAccountTransactionHistoryQuery, PageResponse<AccountTransactionHistoryItem>>
{
    private const string SystemActor = "System";
    private const string UnknownAdminActor = "Unknown Admin";

    public async Task<Result<PageResponse<AccountTransactionHistoryItem>>> Handle(
        GetAccountTransactionHistoryQuery query,
        CancellationToken cancellationToken)
    {
        EducationAccount? account = await educationAccounts.FindByIdAsync(
            query.EducationAccountId,
            cancellationToken);

        if (account is null)
        {
            return Result<PageResponse<AccountTransactionHistoryItem>>.Failure(EducationAccountErrors.NotFound);
        }

        PersonSummary? person = await people.FindAsync(account.PersonId, cancellationToken);
        if (person is null)
        {
            return Result<PageResponse<AccountTransactionHistoryItem>>.Failure(AccountErrors.InvalidPerson);
        }

        if (person.OrganizationId is long organizationId)
        {
            Result access = adminAccess.EnsureCanAccessOrganization(organizationId);
            if (access.IsFailure)
            {
                return Result<PageResponse<AccountTransactionHistoryItem>>.Failure(access.Error);
            }
        }
        else if (!adminAccess.IsHqAdmin)
        {
            return Result<PageResponse<AccountTransactionHistoryItem>>.Failure(AccountErrors.OrganizationOutsideScope);
        }

        HistoryPage<AccountTransactionHistoryProjection> page = await historyReader.GetTransactionsAsync(
            account.Id,
            query.Page,
            query.PageSize,
            cancellationToken);

        long[] actorIds = page.Items
            .Select(x => x.CreatedByLoginAccountId)
            .OfType<long>()
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<long, string> actorNames = actorIds.Length == 0
            ? new Dictionary<long, string>()
            : await loginAccountDisplayDirectory.FindDisplayNamesAsync(actorIds, cancellationToken);

        AccountTransactionHistoryItem[] items = page.Items
            .Select(x => new AccountTransactionHistoryItem(
                x.TransactionId,
                x.TransactionAtUtc,
                ResolveTypeCode(x),
                ResolveTypeLabel(x),
                x.Description,
                x.Amount,
                x.BalanceAfter,
                ResolveActor(x.CreatedByLoginAccountId, actorNames)))
            .ToArray();

        return Result<PageResponse<AccountTransactionHistoryItem>>.Success(
            new PageResponse<AccountTransactionHistoryItem>(
                items,
                query.Page,
                query.PageSize,
                page.TotalCount));
    }

    private static string ResolveActor(
        long? loginAccountId,
        IReadOnlyDictionary<long, string> actorNames)
    {
        if (loginAccountId is null)
        {
            return SystemActor;
        }

        return actorNames.TryGetValue(loginAccountId.Value, out string? displayName)
            && !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : UnknownAdminActor;
    }

    private static string ResolveTypeCode(AccountTransactionHistoryProjection transaction)
        => string.IsNullOrWhiteSpace(transaction.ReferenceTypeCode)
            ? transaction.TransactionTypeCode
            : transaction.ReferenceTypeCode;

    private static string ResolveTypeLabel(AccountTransactionHistoryProjection transaction)
    {
        string typeCode = ResolveTypeCode(transaction);
        return string.Equals(typeCode, "TOPUP", StringComparison.OrdinalIgnoreCase)
            ? "Top-up"
            : typeCode;
    }
}
