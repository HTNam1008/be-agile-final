namespace Moe.Modules.Notifications.IGateway.Notifications;

public sealed record NotificationCreateRequest(
    long RecipientUserAccountId,
    string NotificationTypeCode,
    string Title,
    string Body);
