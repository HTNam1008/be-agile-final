using Moe.Modules.IdentityPlatform.IGateway.Accounts;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;

internal sealed class EducationAccountLookupGateway(EducationAccountDirectory directory) : IEducationAccountLookupGateway
{
    public async Task<EducationAccountLookupSummary?> FindByPersonIdAsync(
        long personId,
        CancellationToken cancellationToken)
    {
        IGateway.Accounts.EducationAccountSummary? account = await directory.FindByPersonIdAsync(personId, cancellationToken);
        return account is null
            ? null
            : new EducationAccountLookupSummary(
                account.EducationAccountId,
                account.PersonId,
                account.AccountNumber,
                account.CurrencyCode,
                account.AccountStatusCode,
                account.CurrentBalance);
    }
}
