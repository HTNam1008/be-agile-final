using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.Application.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountHandler(
    ICurrentUser currentUser,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationRepository notificationRepository) : IQueryHandler<GetUnreadNotificationCountQuery, long>
{
    public async Task<Result<long>> Handle(GetUnreadNotificationCountQuery query, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Result<long>.Failure(NotificationErrors.NotAuthenticated);

        long? userAccountId = currentUser.UserAccountId;
        if (userAccountId is null && currentUser.PersonId is long personId)
        {
            userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, cancellationToken);
        }

        if (userAccountId is null)
            return Result<long>.Failure(NotificationErrors.NotFound);

        long unreadCount = await notificationRepository.GetUnreadCountAsync(userAccountId.Value, cancellationToken);
        return Result<long>.Success(unreadCount);
    }
}
