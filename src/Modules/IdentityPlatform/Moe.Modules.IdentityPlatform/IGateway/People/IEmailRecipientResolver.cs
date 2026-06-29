namespace Moe.Modules.IdentityPlatform.IGateway.People;

public sealed record EmailRecipient(string EmailAddress, string SourceCode);

public static class EmailRecipientSourceCodes
{
    public const string Preferred = "PREFERRED";
    public const string Contact = "CONTACT";
    public const string Official = "OFFICIAL";
    public const string Provided = "PROVIDED";
    public const string DevelopmentFallback = "DEVELOPMENT_FALLBACK";
}

public interface IEmailRecipientResolver
{
    Task<EmailRecipient?> ResolveForPersonAsync(
        long personId,
        CancellationToken cancellationToken);

    EmailRecipient? ResolveProvided(string? emailAddress);
}
