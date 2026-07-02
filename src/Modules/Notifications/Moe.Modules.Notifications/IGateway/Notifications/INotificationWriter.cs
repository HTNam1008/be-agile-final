using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.IGateway.Notifications;

public interface INotificationWriter
{
    Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default);
}
