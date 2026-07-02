using System.Runtime.CompilerServices;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;

namespace Moe.EducationAccountTopUp.UnitTests.TestDoubles;

internal sealed class RecordingEmailNotificationQueue : IEmailNotificationQueue
{
    public List<EmailNotificationJob> Jobs { get; } = [];

    public Result Result { get; set; } = Result.Success();

    public ValueTask<Result> EnqueueAsync(
        EmailNotificationJob job,
        CancellationToken cancellationToken)
    {
        Jobs.Add(job);
        return ValueTask.FromResult(Result);
    }

    public async IAsyncEnumerable<EmailNotificationJob> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }
}
