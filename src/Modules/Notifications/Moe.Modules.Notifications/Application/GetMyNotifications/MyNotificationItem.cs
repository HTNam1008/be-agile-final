namespace Moe.Modules.Notifications.Application.GetMyNotifications;

public sealed record MyNotificationItem(
    long NotificationId,
    string NotificationTypeCode,
    string Title,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string NotificationStatusCode);
