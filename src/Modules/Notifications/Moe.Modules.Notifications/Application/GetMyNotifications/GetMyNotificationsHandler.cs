using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.Application.GetMyNotifications;

public sealed class GetMyNotificationsHandler(
    ICurrentUser currentUser,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationRepository notificationRepository) : IQueryHandler<GetMyNotificationsQuery, PageResponse<MyNotificationItem>>
{
    public async Task<Result<PageResponse<MyNotificationItem>>> Handle(GetMyNotificationsQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Result<PageResponse<MyNotificationItem>>.Failure(NotificationErrors.NotAuthenticated);

        long? userAccountId = currentUser.UserAccountId;
        if (userAccountId is null && currentUser.PersonId is long personId)
        {
            userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, cancellationToken);
        }

        if (userAccountId is null)
            return Result<PageResponse<MyNotificationItem>>.Failure(NotificationErrors.NotFound);

        int take = query.Take <= 0 ? 20 : Math.Min(query.Take, 100);
        var notifications = await notificationRepository.GetMyNotificationsAsync(userAccountId.Value, take, cancellationToken);
        var items = notifications.Select(x => new MyNotificationItem(
            x.Id,
            x.NotificationTypeCode,
            x.Title,
            x.Body,
            x.CreatedAtUtc,
            x.ReadAtUtc,
            x.NotificationStatusCode)).ToArray();

        return Result<PageResponse<MyNotificationItem>>.Success(new PageResponse<MyNotificationItem>(items, 1, items.Length, items.Length));
    }
}
