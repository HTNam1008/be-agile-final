using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;

namespace Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;

public interface IEducationAccountReader
{
    Task<MyEducationAccountDto?> GetMyEducationAccountAsync(long personId, CancellationToken cancellationToken = default);
}
