using FluentValidation;

namespace Moe.Modules.IdentityPlatform.Application.Students.CreateStudent;

public sealed class CreateStudentValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentValidator()
    {
        RuleFor(x => x.SchoolName).MaximumLength(200);

        RuleFor(x => x.IdentityNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(30)
            .Matches("^[A-Za-z0-9]+$")
            .WithMessage("Identity number can contain only letters and numbers.");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DateOfBirth)
            .Must(BeReasonableStudentAge)
            .WithMessage("Student age must be between 6 and 40 years.");

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
}
