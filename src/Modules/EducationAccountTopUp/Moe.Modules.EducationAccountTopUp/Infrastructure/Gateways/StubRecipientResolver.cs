using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.IGateway;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class StubRecipientResolver(
    ILogger<StubRecipientResolver> logger) : IRecipientResolver
{
    public Task<IReadOnlyList<RecipientInfo>> GetRecipientsChunkAsync(
        long campaignId,
        long runId,
        int chunkSize,
        int offset,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub recipient resolver called for campaign {CampaignId}, run {TopUpRunId}, chunk size {ChunkSize}, offset {Offset}",
            campaignId,
            runId,
            chunkSize,
            offset);

        IReadOnlyList<RecipientInfo> empty = Array.Empty<RecipientInfo>();
        return Task.FromResult(empty);
    }

    public Task<int> GetTotalRecipientCountAsync(
        long campaignId,
        long runId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
