using Moe.Modules.Notifications.Application;

namespace Moe.Modules.Notifications.IGateway.Notifications;

public interface INotificationRealtimeNotifier
{
    Task NotifyUserAccountAsync(long userAccountId, NotificationRealtimeMessage message, CancellationToken cancellationToken = default);
}
