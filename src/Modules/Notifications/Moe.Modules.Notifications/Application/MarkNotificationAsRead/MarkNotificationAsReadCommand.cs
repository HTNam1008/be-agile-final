using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Notifications.Application.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(long NotificationId) : ICommand;
