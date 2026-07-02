namespace Moe.Modules.Notifications.Domain.Notifications;

public sealed record NotificationCatalogItem(
    string TypeCode,
    string SourceEpicCode,
    string DefaultPriorityCode,
    string DefaultChannelCodes,
    string TitleTemplate,
    string BodyTemplate);
