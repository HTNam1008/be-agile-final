using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;

namespace Moe.Modules.Notifications.Application.GetMyNotifications;

public sealed record GetMyNotificationsQuery(int Take = 20) : IQuery<PageResponse<MyNotificationItem>>;
