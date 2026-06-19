using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public interface IRecipientProcessingService
{
    Task<Result<RecipientProcessingResult>> ProcessRecipientAsync(
        long topUpRunId,
        long educationAccountId,
        decimal amount,
        long organizationUnitId,
        string campaignReason,
        CancellationToken cancellationToken = default);
}
