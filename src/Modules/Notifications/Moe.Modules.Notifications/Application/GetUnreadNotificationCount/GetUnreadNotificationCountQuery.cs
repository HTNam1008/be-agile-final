using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Notifications.Application.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery : IQuery<long>;
