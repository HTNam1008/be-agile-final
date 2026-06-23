using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

internal sealed class GetMyEducationAccountTransactionsQueryHandler(
    IEducationAccountReader reader)
    : IQueryHandler<GetMyEducationAccountTransactionsQuery, MyEducationAccountTransactionsPage>
{
    public async Task<Result<MyEducationAccountTransactionsPage>> Handle(
        GetMyEducationAccountTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PersonId <= 0)
        {
            return Result<MyEducationAccountTransactionsPage>.Failure(
                EducationAccountErrors.AuthenticatedStudentRequired);
        }

        string? category = NormalizeCategory(query.Category);
        if (category is null && !string.IsNullOrWhiteSpace(query.Category))
        {
            return Result<MyEducationAccountTransactionsPage>.Failure(
                EducationAccountErrors.InvalidTransactionCategory);
        }

        MyEducationAccountTransactionsPage? transactions =
            await reader.GetTransactionsAsync(
                query.PersonId,
                Math.Max(query.Page, 1),
                Math.Clamp(query.PageSize, 1, 100),
                category,
                cancellationToken);

        if (transactions is null)
        {
            return Result<MyEducationAccountTransactionsPage>.Failure(EducationAccountErrors.NotFound);
        }

        return Result<MyEducationAccountTransactionsPage>.Success(transactions);
    }

    private static string? NormalizeCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        string normalized = category.Trim().ToUpperInvariant();
        return normalized is "TOP_UP" or "PAYMENT" or "REFUND" or "REVERSAL"
            ? normalized
            : null;
    }
}
