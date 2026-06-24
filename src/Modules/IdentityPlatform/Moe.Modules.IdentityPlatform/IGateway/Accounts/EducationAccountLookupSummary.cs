namespace Moe.Modules.IdentityPlatform.IGateway.Accounts;

public sealed record EducationAccountLookupSummary(
    long EducationAccountId,
    long PersonId,
    string AccountNumber,
    string CurrencyCode,
    string AccountStatusCode,
    decimal CurrentBalance);
