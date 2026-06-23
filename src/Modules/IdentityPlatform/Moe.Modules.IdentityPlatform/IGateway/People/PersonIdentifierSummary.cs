namespace Moe.Modules.IdentityPlatform.IGateway.People;

public sealed record PersonIdentifierSummary(
    string IdentifierTypeCode,
    string IdentifierValue,
    bool IsPrimary);
