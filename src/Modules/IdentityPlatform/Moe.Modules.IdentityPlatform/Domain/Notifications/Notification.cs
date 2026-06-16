using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Notifications;

internal sealed class Notification : Entity<long>
{
    private Notification() : base(0) { }

    public long RecipientPersonId { get; private set; }
    public long? RecipientLoginAccountId { get; private set; }
    public string NotificationTypeCode { get; private set; } = string.Empty;
    public string ReferenceTypeCode { get; private set; } = string.Empty;
    public long? ReferenceId { get; private set; }
    public string ChannelCode { get; private set; } = string.Empty;
    public string TemplateCode { get; private set; } = string.Empty;
    public string NotificationStatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
}
