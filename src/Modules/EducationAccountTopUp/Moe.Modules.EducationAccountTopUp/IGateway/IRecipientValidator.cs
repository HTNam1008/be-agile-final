using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface IRecipientValidator
{
    Task<Result> ValidateRecipientAsync(
        long educationAccountId,
        long organizationUnitId,
        CancellationToken cancellationToken = default);
}
