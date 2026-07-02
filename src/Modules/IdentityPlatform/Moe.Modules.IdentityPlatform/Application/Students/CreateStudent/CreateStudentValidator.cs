using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

public sealed class CreateStudentValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentValidator()
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

        RuleFor(x => x.DateOfBirth)
            .Must(BeReasonableStudentAge)
            .WithMessage("Student age must be between 6 and 40 years.");

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

        RuleFor(x => x.Mobile).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(1000);
    }

    private static bool BeReasonableStudentAge(DateOnly dateOfBirth)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        int age = today.Year - dateOfBirth.Year;

        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age is >= 6 and <= 40;
    }

    private static bool HaveStartDateOnOrAfterDateOfBirth(CreateStudentCommand command)
        => !command.StartDate.HasValue || command.StartDate.Value >= command.DateOfBirth;
}
