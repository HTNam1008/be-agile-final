using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.MailDelivery.IGateway;
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
        string? contactEmail = await db.Set<UserAccount>()
            .AsNoTracking()
            .Where(account => account.PersonId == personId && account.RoleCode == RoleCodes.Student)
            .OrderByDescending(account => account.Id)
            .Select(account => account.ContactEmail)
            .FirstOrDefaultAsync(cancellationToken);

        return ValidRecipient(contactEmail, EmailRecipientSourceCodes.Contact)
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

}
