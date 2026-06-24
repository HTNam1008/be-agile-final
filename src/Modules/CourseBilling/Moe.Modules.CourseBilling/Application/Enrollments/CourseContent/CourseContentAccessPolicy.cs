using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Application.Enrollments.CourseContent;

internal static class CourseContentAccessPolicy
{
    private static readonly string[] AccessibleStatuses =
    [
        CourseEnrollmentStatusCodes.Active,
        CourseEnrollmentStatusCodes.PaidInFull,
        CourseEnrollmentStatusCodes.Completed
    ];

    public static Result Check(CourseEnrollment enrollment, Course course, DateOnly today)
    {
        if (today < course.StartDate)
            return Result.Failure(CourseBillingErrors.CourseContentNotOpen);

        if (enrollment.ExitAtUtc is not null ||
            !AccessibleStatuses.Contains(enrollment.EnrollmentStatusCode, StringComparer.Ordinal))
        {
            return Result.Failure(CourseBillingErrors.CourseContentLocked);
        }

        return Result.Success();
    }
}
