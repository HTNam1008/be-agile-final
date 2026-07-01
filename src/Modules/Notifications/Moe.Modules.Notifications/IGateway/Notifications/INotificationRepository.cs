using Moe.Modules.Notifications.Domain.Notifications;

namespace Moe.Modules.Notifications.IGateway.Notifications;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> GetMyNotificationsAsync(long userAccountId, int take, CancellationToken cancellationToken);
    Task<long> GetUnreadCountAsync(long userAccountId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
