using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed class ProvisionStudentSingpassAccountRequestValidator : AbstractValidator<ProvisionStudentSingpassAccountRequest>
{
    public ProvisionStudentSingpassAccountRequestValidator()
    {
        RuleFor(x => x.ExternalIssuer).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SingpassSubjectId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(100);
    }
}
