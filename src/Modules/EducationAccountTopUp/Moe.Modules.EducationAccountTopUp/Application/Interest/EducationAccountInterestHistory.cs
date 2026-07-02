using Moe.Application.Abstractions.Messaging;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.Interest;

public sealed record EducationAccountInterestHistoryResponse(
    decimal AnnualInterestRate,
    string CurrencyCode,
    IReadOnlyCollection<EducationAccountInterestHistoryItem> Items);

public sealed record EducationAccountInterestHistoryItem(
    int Year,
    decimal OpeningBalance,
    decimal InterestAmount,
    decimal ClosingBalance,
    DateTime CreditedAtUtc);

public sealed record GetMyEducationAccountInterestHistoryQuery(long PersonId)
    : IQuery<EducationAccountInterestHistoryResponse>;

internal sealed class GetMyEducationAccountInterestHistoryHandler(
    IEducationAccountInterestHistoryReader reader)
    : IQueryHandler<GetMyEducationAccountInterestHistoryQuery, EducationAccountInterestHistoryResponse>
{
    public async Task<Result<EducationAccountInterestHistoryResponse>> Handle(
        GetMyEducationAccountInterestHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (query.PersonId <= 0)
        {
            return Result<EducationAccountInterestHistoryResponse>.Failure(
                Domain.EducationAccounts.EducationAccountErrors.AuthenticatedStudentRequired);
        }

        EducationAccountInterestHistoryResponse? history =
            await reader.GetMyInterestHistoryAsync(query.PersonId, cancellationToken);

        return history is null
            ? Result<EducationAccountInterestHistoryResponse>.Failure(Domain.EducationAccounts.EducationAccountErrors.NotFound)
            : Result<EducationAccountInterestHistoryResponse>.Success(history);
    }
}

internal interface IEducationAccountInterestHistoryReader
{
    Task<EducationAccountInterestHistoryResponse?> GetMyInterestHistoryAsync(
        long personId,
        CancellationToken cancellationToken = default);
}
