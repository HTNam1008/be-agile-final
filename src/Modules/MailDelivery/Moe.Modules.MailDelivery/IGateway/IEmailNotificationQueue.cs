using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.IGateway;

public interface IEmailNotificationQueue
{
    ValueTask<Result> EnqueueAsync(
        EmailNotificationJob job,
        CancellationToken cancellationToken);

    IAsyncEnumerable<EmailNotificationJob> ReadAllAsync(CancellationToken cancellationToken);
}
