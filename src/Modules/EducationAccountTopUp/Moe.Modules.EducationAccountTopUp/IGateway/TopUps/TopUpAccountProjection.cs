namespace Moe.Modules.EducationAccountTopUp.IGateway.TopUps;

internal sealed record TopUpAccountProjection(
    long PersonId,
    long EducationAccountId,
    string AccountNumber,
    string AccountStatusCode,
    decimal Balance);
