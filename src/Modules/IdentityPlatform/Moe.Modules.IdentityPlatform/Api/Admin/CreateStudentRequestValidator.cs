using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Api.Admin;

public sealed class CreateStudentRequestValidator : AbstractValidator<CreateStudentRequest>
{
    public CreateStudentRequestValidator()
    {
        RuleFor(x => x.SchoolName).MaximumLength(200);
        RuleFor(x => x.OrganizationId)
            .GreaterThan(0)
            .When(x => x.OrganizationId.HasValue);

        RuleFor(x => x.IdentityNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(30)
            .Matches("^[A-Za-z0-9]+$")
            .WithMessage("Identity number can contain only letters and numbers.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth).NotEmpty();

        RuleFor(x => x.NationalityCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.CitizenshipStatusCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.StudentNumber)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.AcademicYear)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.LevelCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.ClassCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Mobile).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(1000);
    }
}
