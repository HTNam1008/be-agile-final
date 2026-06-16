namespace Moe.Modules.EducationAccountTopUp.Application.OpenAccount;

public sealed record OpenManualAccountResponse(
    long EducationAccountId,
    string AccountNumber,
    string StatusCode);
