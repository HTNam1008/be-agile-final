using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;

public sealed class ProvisionStudentSingpassAccountValidator : AbstractValidator<ProvisionStudentSingpassAccountCommand>
{
    public ProvisionStudentSingpassAccountValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0);
        RuleFor(x => x.ExternalIssuer).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SingpassSubjectId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}
