using Moe.SharedKernel.Domain;

namespace Moe.Modules.Notifications.Domain.Notifications;

public sealed class Notification : Entity<long>
{
    private Notification() : base(0) { }

    public Notification(long recipientUserAccountId, string notificationTypeCode, string referenceTypeCode, string channelCode, string templateCode, string title, string body, DateTime createdAtUtc)
        : base(0)
    {
        if (!NotificationCatalog.IsKnown(notificationTypeCode))
            throw new ArgumentOutOfRangeException(nameof(notificationTypeCode), notificationTypeCode, "Unknown notification type code.");

        RecipientUserAccountId = recipientUserAccountId;
        NotificationTypeCode = notificationTypeCode;
        ReferenceTypeCode = referenceTypeCode;
        ChannelCode = channelCode;
        TemplateCode = templateCode;
        Title = title;
        Body = body;
        CreatedAtUtc = createdAtUtc;
        NotificationStatusCode = "UNREAD";
    }

    public long RecipientUserAccountId { get; private set; }
    public string NotificationTypeCode { get; private set; } = string.Empty;
    public string ReferenceTypeCode { get; private set; } = string.Empty;
    public string ChannelCode { get; private set; } = string.Empty;
    public string TemplateCode { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public string NotificationStatusCode { get; private set; } = string.Empty;

    public void MarkAsRead(DateTime readAtUtc)
    {
        ReadAtUtc ??= readAtUtc;
        NotificationStatusCode = "READ";
    }
}
