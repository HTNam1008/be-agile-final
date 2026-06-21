using Moe.Application.Abstractions.Messaging;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

internal sealed class GetMyEducationAccountQueryHandler(
    IEducationAccountReader reader) : IQueryHandler<GetMyEducationAccountQuery, MyEducationAccountDto>
{
    public async Task<Result<MyEducationAccountDto>> Handle(
        GetMyEducationAccountQuery query,
        CancellationToken cancellationToken)
    {
        var account = await reader.GetMyEducationAccountAsync(query.PersonId, cancellationToken);

        if (account is null)
        {
            return Result<MyEducationAccountDto>.Failure(EducationAccountErrors.NotFound);
        }

        return Result<MyEducationAccountDto>.Success(account);
    }
}
