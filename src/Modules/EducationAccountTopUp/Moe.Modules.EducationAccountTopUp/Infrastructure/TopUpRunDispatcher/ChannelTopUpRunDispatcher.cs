using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.IGateway;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;

public sealed class ChannelTopUpRunDispatcher(
    ILogger<ChannelTopUpRunDispatcher> logger) : ITopUpRunDispatcher, ITopUpRunQueueReader
{
    private readonly Channel<long> _channel = Channel.CreateBounded<long>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    });

    public ChannelReader<long> Reader => _channel.Reader;

    public async Task EnqueueAsync(long topUpRunId, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(topUpRunId, cancellationToken);
        logger.LogInformation(
            "Top-up run {TopUpRunId} enqueued for background processing",
            topUpRunId);
    }
}
