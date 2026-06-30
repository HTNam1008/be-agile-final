using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.History.AccountFlatHistory;

public sealed record GetAccountFlatHistoryQuery(
    long EducationAccountId,
    int Page = 1,
    int PageSize = 20) : IQuery<AccountFlatHistoryResponse>;
