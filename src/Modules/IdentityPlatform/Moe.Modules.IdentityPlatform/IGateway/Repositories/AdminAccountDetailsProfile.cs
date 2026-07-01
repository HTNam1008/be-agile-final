namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal sealed record AdminAccountDetailsProfile(
    long PersonId,
    string PersonStatusCode,
    long? UserAccountId,
    string? UserAccountStatusCode,
    string? IdentityNumberMasked,
    string OfficialFullName,
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
    DateTime UpdatedAtUtc);
