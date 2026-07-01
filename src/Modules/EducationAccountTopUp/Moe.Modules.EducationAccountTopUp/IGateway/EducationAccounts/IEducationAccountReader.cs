using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

namespace Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;

public interface IEducationAccountReader
{
    Task<MyEducationAccountDto?> GetMyEducationAccountAsync(long personId, CancellationToken cancellationToken = default);

    Task<MyEducationAccountTransactionsPage?> GetTransactionsAsync(
        long personId,
        int page,
        int pageSize,
        string? category,
        string? sortBy,
        string? sortDirection,
        CancellationToken cancellationToken = default);
}
