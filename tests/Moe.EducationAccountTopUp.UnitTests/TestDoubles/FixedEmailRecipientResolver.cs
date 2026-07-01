using Moe.Modules.MailDelivery.IGateway;

namespace Moe.EducationAccountTopUp.UnitTests.TestDoubles;

internal sealed class FixedEmailRecipientResolver(
    string? emailAddress = "student.real@example.com") : IEmailRecipientResolver
{
    public Task<EmailRecipient?> ResolveForPersonAsync(
        long personId,
        CancellationToken cancellationToken)
        => Task.FromResult(emailAddress is null
            ? null
            : new EmailRecipient(emailAddress, EmailRecipientSourceCodes.Contact));

    public EmailRecipient? ResolveProvided(string? providedEmail)
        => string.IsNullOrWhiteSpace(providedEmail)
            ? null
            : new EmailRecipient(providedEmail, EmailRecipientSourceCodes.Provided);
}
