namespace Moe.Modules.IdentityPlatform.IGateway.People;

public sealed record PersonSummary(
    long PersonId,
    string DisplayName,
    DateOnly DateOfBirth,
    string NationalityCode,
    string CitizenshipStatusCode,
    long? OrganizationId);
