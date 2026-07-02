namespace Moe.Modules.Notifications.Application;

public sealed record NotificationRealtimeMessage(
    long NotificationId,
    string NotificationTypeCode,
    string Title,
    string Body);
