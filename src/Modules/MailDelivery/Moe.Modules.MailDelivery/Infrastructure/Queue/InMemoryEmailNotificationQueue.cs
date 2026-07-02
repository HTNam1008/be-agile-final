using System.Threading.Channels;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Infrastructure.Queue;

internal sealed class InMemoryEmailNotificationQueue : IEmailNotificationQueue
{
    private readonly Channel<EmailNotificationJob> _channel = Channel.CreateBounded<EmailNotificationJob>(
        new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask<Result> EnqueueAsync(
        EmailNotificationJob job,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool queued = _channel.Writer.TryWrite(job);
        return ValueTask.FromResult(queued
            ? Result.Success()
            : Result.Failure(MailDeliveryErrors.QueueFull));
    }

    public IAsyncEnumerable<EmailNotificationJob> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
