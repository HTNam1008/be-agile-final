using Moe.Modules.CourseBilling.Application.AdminCourses.Fees;
using Moe.Modules.CourseBilling.Application.AdminCourses.Materials;
using Moe.Modules.CourseBilling.Contracts.AdminCourses;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;

namespace Moe.Modules.CourseBilling.Application.AdminCourses.Courses;

internal static class CourseMapper
{
    public static CourseDetailDto ToDetail(CourseAggregate aggregate)
    {
        IReadOnlyList<CourseMaterialDto> materials = aggregate.Materials
            .Select(CourseMaterialMapper.ToMaterialDto)
            .ToArray();
        IReadOnlyList<CourseFeeDto> fees = aggregate.Fees
            .Select(CourseFeeMapper.ToFeeDto)
            .ToArray();

        return new CourseDetailDto(
            aggregate.Course.Id,
            aggregate.Course.OrganizationId,
            aggregate.Course.CourseCode,
            aggregate.Course.CourseName,
            aggregate.Course.Description,
            aggregate.Course.StartDate,
            aggregate.Course.EndDate,
            aggregate.Course.EnrollmentOpenAtUtc,
            aggregate.Course.EnrollmentCloseAtUtc,
            aggregate.Course.BeforeStartRefundPercentage,
            aggregate.Course.AfterStartRefundPercentage,
            aggregate.Course.CourseStatusCode,
            aggregate.Course.UpdatedAtUtc,
            aggregate.Course.DisabledAtUtc,
            materials,
            fees,
            aggregate.EnrollmentSummary,
            BuildReadiness(aggregate));
    }

    public static CoursePublishReadinessDto BuildReadiness(CourseAggregate aggregate)
    {
        List<string> errors = [];
        List<string> warnings = [];
        Course course = aggregate.Course;

        if (string.IsNullOrWhiteSpace(course.CourseCode)) errors.Add("Course code is required.");
        if (string.IsNullOrWhiteSpace(course.CourseName)) errors.Add("Course name is required.");
        if (course.IsDisabled) errors.Add("Course must not be disabled.");
        if (course.StartDate > course.EndDate) errors.Add("Start date cannot be after end date.");
        if (course.EnrollmentOpenAtUtc > course.EnrollmentCloseAtUtc) errors.Add("Enrollment open date cannot be after enrollment close date.");
        if (course.EnrollmentCloseAtUtc >= course.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) errors.Add("Enrollment must close before the course start date.");
        if (!aggregate.Materials.Any(x => x.IsActive)) warnings.Add("No material uploaded.");
        if (!aggregate.Fees.Any(x => x.CourseFee.IsActive)) errors.Add("At least one active course fee is required.");

        string step = course.IsDisabled
            ? "DISABLED"
            : course.IsPublished
                ? "PUBLISHED"
                : aggregate.Fees.Any(x => x.CourseFee.IsActive)
                    ? "READY_TO_PUBLISH"
                    : aggregate.Materials.Any(x => x.IsActive)
                        ? "FEES"
                        : "MATERIALS";

        return new CoursePublishReadinessDto(errors.Count == 0, errors, warnings, step);
    }
}
