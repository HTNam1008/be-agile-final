namespace Moe.Modules.MailDelivery.IGateway;

public sealed record EmailRecipient(string EmailAddress, string SourceCode);

public static class EmailRecipientSourceCodes
{
    public const string Contact = "CONTACT";
    public const string Official = "OFFICIAL";
    public const string DevelopmentFallback = "DEVELOPMENT_FALLBACK";
}

public interface IEmailRecipientResolver
{
    Task<EmailRecipient?> ResolveForPersonAsync(
        long personId,
        CancellationToken cancellationToken);
}
