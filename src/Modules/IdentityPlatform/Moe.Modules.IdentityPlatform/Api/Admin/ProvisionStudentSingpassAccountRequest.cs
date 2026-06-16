namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed record ProvisionStudentSingpassAccountRequest(
    string ExternalIssuer,
    string SingpassSubjectId,
    string DisplayName,
    string IdempotencyKey);
