using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;

public interface ITopUpAccountSelectionResolver
{
    Task<Result<TopUpAccountSelectionResolution>> ResolveAsync(
        TopUpAccountSelection selection,
        CancellationToken cancellationToken);
}
