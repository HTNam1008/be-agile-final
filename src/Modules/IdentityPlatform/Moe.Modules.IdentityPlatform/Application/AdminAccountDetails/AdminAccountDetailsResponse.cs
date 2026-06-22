namespace Moe.Modules.IdentityPlatform.Application.AdminAccountDetails;

public sealed record AdminAccountDetailsResponse(
    long PersonId,
    long EducationAccountId,
    string AccountNumber,
    string? IdentityNumberMasked,
    string FullName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string? MailingAddress,
    string? ResidentialAddress,
    string? Email,
    string? ContactNumber,
    long? SchoolOrganizationId,
    string? SchoolOrganizationCode,
    string? SchoolOrganizationName,
    string? AcademicYear,
    string? LevelCode,
    string? ClassCode,
    string AccountStatusCode,
    decimal CurrentBalance,
    DateTime ExpectedUpdatedAtUtc);
