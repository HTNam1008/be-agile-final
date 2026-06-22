using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseEnrollment : Entity<long>
{
    private CourseEnrollment() : base(0) { }

    private CourseEnrollment(
        long personId,
        long courseId,
        string enrollmentSourceCode,
        long enrolledByLoginAccountId,
        DateTime enrolledAtUtc) : base(0)
    {
        PersonId = personId;
        CourseId = courseId;
        EnrollmentSourceCode = enrollmentSourceCode;
        EnrolledByLoginAccountId = enrolledByLoginAccountId;
        EnrolledAtUtc = enrolledAtUtc;
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.PendingPayment;
    }

    public long PersonId { get; private set; }
    public long CourseId { get; private set; }
    public string EnrollmentSourceCode { get; private set; } = string.Empty;
    public long EnrolledByLoginAccountId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public string EnrollmentStatusCode { get; private set; } = string.Empty;
    public DateTime? ExitAtUtc { get; private set; }
    public string? ExitReasonCode { get; private set; }

    public static Result<CourseEnrollment> EnrollByAdmin(
        long personId,
        long courseId,
        long adminLoginAccountId,
        DateTime enrolledAtUtc)
    {
        Result validation = ValidateEnrollment(personId, courseId, adminLoginAccountId);
        if (validation.IsFailure)
        {
            return Result<CourseEnrollment>.Failure(validation.Error);
        }

        CourseEnrollment enrollment = new(
            personId,
            courseId,
            CourseEnrollmentSourceCodes.AdminAdd,
            adminLoginAccountId,
            enrolledAtUtc);

        return Result<CourseEnrollment>.Success(enrollment);
    }

    public static Result<CourseEnrollment> JoinSelf(
        long personId,
        long courseId,
        long loginAccountId,
        DateTime enrolledAtUtc)
    {
        Result validation = ValidateEnrollment(personId, courseId, loginAccountId);
        if (validation.IsFailure)
        {
            return Result<CourseEnrollment>.Failure(validation.Error);
        }

        CourseEnrollment enrollment = new(
            personId,
            courseId,
            CourseEnrollmentSourceCodes.SelfJoin,
            loginAccountId,
            enrolledAtUtc);

        return Result<CourseEnrollment>.Success(enrollment);
    }

    public void Cancel(DateTime utcNow)
    {
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.Cancelled;
        ExitAtUtc = utcNow;
        ExitReasonCode = "ADMIN_REMOVED";
    }

    private static Result ValidateEnrollment(long personId, long courseId, long enrolledByLoginAccountId)
    {
        if (personId <= 0)
        {
            return Result.Failure(CourseBillingErrors.InvalidPerson);
        }

        if (courseId <= 0)
        {
            return Result.Failure(CourseBillingErrors.InvalidCourse);
        }

        if (enrolledByLoginAccountId <= 0)
        {
            return Result.Failure(CourseBillingErrors.ActorRequired);
        }

        return Result.Success();
    }
}

public static class CourseEnrollmentSourceCodes
{
    public const string AdminAdd = "ADMIN_ADD";
    public const string SelfJoin = "SELF_JOIN";
}



public static class CourseBillingErrors
{
    public static readonly Error InvalidPerson = new("COURSE.INVALID_PERSON", "A valid person is required.");
    public static readonly Error InvalidCourse = new("COURSE.INVALID_COURSE", "A valid course is required.");
    public static readonly Error PersonNotFound = new("COURSE.PERSON_NOT_FOUND", "The person was not found.");
    public static readonly Error CourseNotFound = new("COURSE.NOT_FOUND", "The course was not found.");
    public static readonly Error DuplicateEnrollment = new("COURSE.ENROLLMENT_DUPLICATE", "The person is already enrolled in this course.");
    public static readonly Error ActorRequired = new("COURSE.ACTOR_REQUIRED", "An authenticated login account is required.");
    public static readonly Error StudentIdentityRequired = new("COURSE.STUDENT_IDENTITY_REQUIRED", "An authenticated student identity is required.");
    public static readonly Error CourseFeesNotConfigured = new("COURSE.FEES_NOT_CONFIGURED", "The course has no active fee lines to bill.");
    public static readonly Error PersonNotInCourseOrganization = new("COURSE.PERSON_NOT_IN_ORGANIZATION", "The person is not actively enrolled in the course organization.");
    public static readonly Error CourseOrganizationForbidden = new("COURSE.ORGANIZATION_FORBIDDEN", "The administrator cannot manage courses in this organization.");
    public static readonly Error OrganizationOutsideScope = new("AUTH.ORGANIZATION_OUTSIDE_SCOPE", "The requested organization is outside the current admin's scope.");
}
