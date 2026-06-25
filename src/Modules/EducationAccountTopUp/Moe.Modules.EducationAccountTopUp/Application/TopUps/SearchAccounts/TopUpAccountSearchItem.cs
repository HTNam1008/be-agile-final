namespace Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;

public sealed record TopUpAccountSearchItem(
    long EducationAccountId,
    long PersonId,
    string MaskedAccountNumber,
    string StudentNumber,
    string DisplayName,
    int Age,
    string AccountStatusCode,
    decimal Balance,
    string SchoolingStatusCode,
    string LevelCode,
    string? ClassCode,
    long OrganizationId);
