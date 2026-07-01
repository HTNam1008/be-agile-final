using Moe.Modules.Notifications.Application;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.StudentFinance.Persistence;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.Infrastructure.Notifications;

public sealed class NotificationWriter(
    MoeDbContext dbContext,
    INotificationRealtimeNotifier realtimeNotifier) : INotificationWriter
{
    public async Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!NotificationCatalog.IsKnown(request.NotificationTypeCode))
            return Result<long>.Failure(new Error("notification.unknown_type", $"Unknown notification type code '{request.NotificationTypeCode}'."));

        var notification = new Notification(
            request.RecipientUserAccountId,
            request.NotificationTypeCode,
            NotificationCatalog.Get(request.NotificationTypeCode).SourceEpicCode,
            NotificationCatalog.GetDefaultChannelCode(request.NotificationTypeCode),
            NotificationCatalog.Get(request.NotificationTypeCode).TypeCode,
            request.Title,
            request.Body,
            DateTime.UtcNow);

        dbContext.Set<Notification>().Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);

        await realtimeNotifier.NotifyUserAccountAsync(
            request.RecipientUserAccountId,
            new NotificationRealtimeMessage(
                notification.Id,
                notification.NotificationTypeCode,
                notification.Title,
                notification.Body),
            cancellationToken);

        return Result<long>.Success(notification.Id);
    }
}
