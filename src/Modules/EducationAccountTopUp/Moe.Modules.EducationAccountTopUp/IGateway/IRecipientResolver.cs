using Moe.Modules.EducationAccountTopUp.Application.RunExecution;

namespace Moe.Modules.EducationAccountTopUp.IGateway;

public interface IRecipientResolver
{
    Task<IReadOnlyList<RecipientInfo>> GetRecipientsChunkAsync(
        long campaignId,
        long runId,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken = default);

    Task<int> GetTotalRecipientCountAsync(
        long campaignId,
        long runId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalResolvedAmountAsync(
        long campaignId,
        long runId,
        CancellationToken cancellationToken = default);
}
