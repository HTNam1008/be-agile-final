using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.IGateway.Students;

namespace Moe.Modules.Notifications.Api.Notifications;

[Authorize(AuthenticationSchemes = $"{AuthenticationSchemes.AdminEntra},{AuthenticationSchemes.EServiceSingpass}")]
public sealed class NotificationHub(IStudentNotificationRecipientResolver notificationRecipients) : Hub
{
    public const string NotificationReceivedMethodName = "notificationReceived";

    public static string UserAccountGroupName(long userAccountId) => $"user-account:{userAccountId}";

    public override async Task OnConnectedAsync()
    {
        long? userAccountId = Context.User?.FindFirst(ClaimNames.UserAccountId) is { } claim && long.TryParse(claim.Value, out long parsed)
            ? parsed
            : null;

        if (userAccountId is null && Context.User?.FindFirst(ClaimNames.PersonId) is { } personClaim && long.TryParse(personClaim.Value, out long personId))
        {
            userAccountId = await notificationRecipients.FindUserAccountIdByPersonIdAsync(personId, Context.ConnectionAborted);
        }

        if (userAccountId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserAccountGroupName(userAccountId.Value));
        }

        await base.OnConnectedAsync();
    }
}
