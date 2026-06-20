namespace Moe.Modules.EducationAccountTopUp.IGateway.Accounts;

public sealed record EducationAccountSummary(
    long EducationAccountId,
    long PersonId,
    string AccountNumber,
    string CurrencyCode,
    string AccountStatusCode,
    decimal CurrentBalance);
