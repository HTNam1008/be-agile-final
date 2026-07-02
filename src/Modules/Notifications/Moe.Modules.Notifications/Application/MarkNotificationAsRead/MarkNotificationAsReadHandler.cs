using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.Application.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadHandler(
    ICurrentUser currentUser,
    IClock clock,
    IStudentNotificationRecipientResolver notificationRecipients,
    INotificationRepository notificationRepository) : ICommandHandler<MarkNotificationAsReadCommand>
{
    public async Task<Result> Handle(MarkNotificationAsReadCommand command, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
            return Result.Failure(NotificationErrors.NotAuthenticated);

        long? userAccountId = currentUser.UserAccountId;
        if (userAccountId is null && currentUser.PersonId is long personId)
        {
            userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, cancellationToken);
        }

        if (userAccountId is null)
            return Result.Failure(NotificationErrors.NotFound);

        var notification = await notificationRepository.GetByIdAsync(command.NotificationId, cancellationToken);
        if (notification is null)
            return Result.Failure(NotificationErrors.NotFound);

        if (notification.RecipientUserAccountId != userAccountId.Value)
            return Result.Failure(NotificationErrors.Forbidden);

        notification.MarkAsRead(clock.UtcNow.UtcDateTime);
        await notificationRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
