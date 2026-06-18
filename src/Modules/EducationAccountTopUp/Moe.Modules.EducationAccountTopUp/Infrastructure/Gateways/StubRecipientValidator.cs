using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class StubRecipientValidator : IRecipientValidator
{
    public Task<Result> ValidateRecipientAsync(
        long educationAccountId,
        long organizationUnitId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
