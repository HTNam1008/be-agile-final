using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;

public sealed record TopUpTransactionResultFilter(
    string? Status,
    string? StudentOrAccountSearch,
    string? Reason,
    DateTime? DateFromUtc,
    DateTime? DateToUtc);

public sealed record GetTopUpTransactionResultsQuery(
    long RunId,
    TopUpTransactionResultFilter Filter,
    int Page,
    int PageSize) : IQuery<PageResponse<TopUpTransactionResultItem>>;

public sealed record TopUpTransactionResultItem(
    long TransactionId,
    long EducationAccountId,
    string MaskedAccountNumber,
    string? MaskedStudentNumber,
    string StudentDisplayName,
    decimal Amount,
    string CurrencyCode,
    string Status,
    string? Reason,
    long? AccountTransactionId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
