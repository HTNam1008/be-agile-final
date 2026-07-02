using Moe.Modules.MailDelivery.IGateway;

namespace Moe.CourseBilling.UnitTests.TestDoubles;

internal sealed class RecordingEmailNotificationScheduler(
    IEmailNotificationQueue queue,
    IEmailDeliverySwitch? mailSwitch = null) : IEmailNotificationScheduler
{
    private readonly IEmailDeliverySwitch _mailSwitch = mailSwitch ?? new FixedEmailDeliverySwitch();

    public bool IsEnabled => _mailSwitch.IsEnabled;

    public async Task<bool> EnqueueForPersonAsync(
        string notificationType,
        long personId,
        string subject,
        string plainTextBody,
        string? htmlBody,
        string? entityType,
        string? entityId,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var result = await queue.EnqueueAsync(
            EmailNotificationJob.ForPerson(
                notificationType,
                personId,
                subject,
                plainTextBody,
                htmlBody,
                entityType,
                entityId),
            cancellationToken);
        return result.IsSuccess;
    }
}
