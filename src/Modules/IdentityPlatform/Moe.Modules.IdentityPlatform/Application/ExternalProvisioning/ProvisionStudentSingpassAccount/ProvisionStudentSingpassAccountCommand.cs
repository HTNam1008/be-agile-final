using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;

public sealed record ProvisionStudentSingpassAccountCommand(
    long PersonId,
    string ExternalIssuer,
    string SingpassSubjectId,
    string DisplayName,
    string IdempotencyKey) : ICommand<ProvisionStudentSingpassAccountResponse>;
