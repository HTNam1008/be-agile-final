using Moe.SharedKernel.Results;

namespace Moe.Modules.Notifications.Application;

internal static class NotificationErrors
{
    public static readonly Error NotAuthenticated = new("notification.not_authenticated", "You are not authenticated.");
    public static readonly Error NotFound = new("notification.not_found", "Notification was not found.");
    public static readonly Error Forbidden = new("notification.forbidden", "You are not allowed to access this notification.");
}
