using Microsoft.AspNetCore.SignalR;
using Moe.Modules.Notifications.Api.Notifications;
using Moe.Modules.Notifications.Application;
using Moe.Modules.Notifications.IGateway.Notifications;

namespace Moe.Modules.Notifications.Infrastructure.Notifications;

public sealed class SignalRNotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext) : INotificationRealtimeNotifier
{
    public Task NotifyUserAccountAsync(long userAccountId, NotificationRealtimeMessage message, CancellationToken cancellationToken = default)
        => hubContext.Clients.Group(NotificationHub.UserAccountGroupName(userAccountId))
            .SendAsync(NotificationHub.NotificationReceivedMethodName, message, cancellationToken);
}
