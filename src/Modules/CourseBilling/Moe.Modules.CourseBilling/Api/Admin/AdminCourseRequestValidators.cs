using FluentValidation;
using Moe.Modules.CourseBilling.Application.AdminFeeComponents;
using Moe.Modules.CourseBilling.Application.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;

namespace Moe.Modules.CourseBilling.Api.Admin;

internal sealed class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.CourseCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CourseName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class UpdateCourseRequestValidator : AbstractValidator<UpdateCourseRequest>
{
    public UpdateCourseRequestValidator()
    {
        RuleFor(x => x.CourseCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CourseName).NotEmpty().MaximumLength(200);
    }
}

internal sealed class CreateFeeComponentRequestValidator : AbstractValidator<CreateFeeComponentRequest>
{
    public CreateFeeComponentRequestValidator()
    {
        RuleFor(x => x.ComponentCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ComponentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ComponentTypeCode)
            .NotEmpty()
            .MaximumLength(30)
            .Must(value => FeeComponentTypeCodes.All.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("Fee component type must be TUITION, MATERIAL or TAX.");
        RuleFor(x => x.CalculationTypeCode).NotEmpty().MaximumLength(30);
    }
}

internal sealed class UpdateFeeComponentRequestValidator : AbstractValidator<UpdateFeeComponentRequest>
{
    public UpdateFeeComponentRequestValidator()
    {
        RuleFor(x => x.ComponentCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ComponentName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ComponentTypeCode)
            .NotEmpty()
            .MaximumLength(30)
            .Must(value => FeeComponentTypeCodes.All.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("Fee component type must be TUITION, MATERIAL or TAX.");
        RuleFor(x => x.CalculationTypeCode).NotEmpty().MaximumLength(30);
    }
}

internal sealed class CreateCourseMaterialRequestValidator : AbstractValidator<CreateCourseMaterialRequest>
{
    public CreateCourseMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialTypeCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCourseMaterialRequestValidator : AbstractValidator<UpdateCourseMaterialRequest>
{
    public UpdateCourseMaterialRequestValidator()
    {
        RuleFor(x => x.MaterialTitle).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaterialTypeCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class CreateCourseFeeRequestValidator : AbstractValidator<CreateCourseFeeRequest>
{
    public CreateCourseFeeRequestValidator()
    {
        RuleFor(x => x.FeeComponentId).GreaterThan(0);
        RuleFor(x => x.FeeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SequenceNumber).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCourseFeeRequestValidator : AbstractValidator<UpdateCourseFeeRequest>
{
    public UpdateCourseFeeRequestValidator()
    {
        RuleFor(x => x.FeeValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SequenceNumber).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AssignStudentsToCourseRequestValidator : AbstractValidator<AssignStudentsToCourseRequest>
{
    public AssignStudentsToCourseRequestValidator()
    {
        RuleFor(x => x.PersonIds).NotEmpty();
    }
}
