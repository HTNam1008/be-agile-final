namespace Moe.Modules.MailDelivery.IGateway;

public sealed record EmailDeliveryMessage(
    string ToEmail,
    string Subject,
    string PlainTextBody,
    string? HtmlBody = null);
