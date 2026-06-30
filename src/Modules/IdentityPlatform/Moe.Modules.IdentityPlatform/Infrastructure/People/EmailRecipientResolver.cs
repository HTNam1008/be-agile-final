using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

public sealed class EmailRecipientResolver(
    MoeDbContext db,
    IHostEnvironment environment,
    IOptions<MailDeliveryOptions> mailOptions) : IEmailRecipientResolver
{
    public async Task<EmailRecipient?> ResolveForPersonAsync(
        long personId,
        CancellationToken cancellationToken)
    {
        RecipientCandidates? candidates = await db.Set<Person>()
            .AsNoTracking()
            .Where(person => person.Id == personId)
            .Select(person => new RecipientCandidates(
                person.PreferredEmail,
                db.Set<UserAccount>()
                    .AsNoTracking()
                    .Where(account => account.PersonId == person.Id && account.RoleCode == RoleCodes.Student)
                    .OrderByDescending(account => account.Id)
                    .Select(account => account.ContactEmail)
                    .FirstOrDefault(),
                person.OfficialEmail))
            .SingleOrDefaultAsync(cancellationToken);

        if (candidates is null)
        {
            return DevelopmentFallback();
        }

        return ValidRecipient(candidates.PreferredEmail, EmailRecipientSourceCodes.Preferred)
            ?? ValidRecipient(candidates.ContactEmail, EmailRecipientSourceCodes.Contact)
            ?? ValidRecipient(candidates.OfficialEmail, EmailRecipientSourceCodes.Official)
            ?? DevelopmentFallback();
    }

    public EmailRecipient? ResolveProvided(string? emailAddress)
        => ValidRecipient(emailAddress, EmailRecipientSourceCodes.Provided)
            ?? DevelopmentFallback();

    private EmailRecipient? DevelopmentFallback()
        => environment.IsDevelopment()
            ? ValidRecipient(
                mailOptions.Value.DevelopmentFallbackRecipient,
                EmailRecipientSourceCodes.DevelopmentFallback)
            : null;

    private static EmailRecipient? ValidRecipient(string? candidate, string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        string trimmed = candidate.Trim();
        return MailAddress.TryCreate(trimmed, out MailAddress? address)
            && string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase)
                ? new EmailRecipient(trimmed, sourceCode)
                : null;
    }

    private sealed record RecipientCandidates(
        string? PreferredEmail,
        string? ContactEmail,
        string? OfficialEmail);
}
