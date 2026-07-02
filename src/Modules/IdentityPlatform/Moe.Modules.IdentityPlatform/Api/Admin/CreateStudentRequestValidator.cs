using FluentValidation;
using Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

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
            .Must(SingaporeIdentityNumberValidator.IsValid)
            .WithMessage("Identity number must be a valid Singapore NRIC/FIN.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth).NotEmpty();

        RuleFor(x => x)
            .Must(HaveStartDateOnOrAfterDateOfBirth)
            .WithMessage("Start date cannot be earlier than date of birth.");

        RuleFor(x => x.NationalityCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.CitizenshipStatusCode)
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.CitizenshipStatusCode));

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
            .MaximumLength(30)
            .When(x => !string.IsNullOrWhiteSpace(x.ClassCode));

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.ContactNumber).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(1000);
    }

    private static bool HaveStartDateOnOrAfterDateOfBirth(CreateStudentRequest request)
        => !request.StartDate.HasValue || request.StartDate.Value >= request.DateOfBirth;
}
