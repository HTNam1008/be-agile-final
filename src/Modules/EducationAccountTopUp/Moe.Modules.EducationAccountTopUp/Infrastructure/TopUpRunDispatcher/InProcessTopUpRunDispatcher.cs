using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.IGateway;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;

internal sealed class InProcessTopUpRunDispatcher(
    ILogger<InProcessTopUpRunDispatcher> logger) : ITopUpRunDispatcher
{
    public Task EnqueueAsync(long topUpRunId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Top-up run {RunId} enqueued for background processing",
            topUpRunId);

        return Task.CompletedTask;
    }
}
