using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

public sealed record GetMyEducationAccountTransactionsQuery(
    long PersonId,
    int Page,
    int PageSize,
    string? Category) : IQuery<MyEducationAccountTransactionsPage>;
