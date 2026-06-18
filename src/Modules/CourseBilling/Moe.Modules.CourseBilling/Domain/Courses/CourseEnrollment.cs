using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseEnrollment : Entity<long>
{
    private CourseEnrollment() : base(0) { }

    public CourseEnrollment(long personId, long courseId, DateTime utcNow) : base(0)
    {
        PersonId = personId;
        CourseId = courseId;
        EnrollmentSourceCode = CourseEnrollmentSourceCodes.SelfService;
        EnrolledByLoginAccountId = 0;
        EnrolledAtUtc = utcNow;
        EnrollmentStatusCode = CourseEnrollmentStatusCodes.PendingPayment;
    }

    public long PersonId { get; private set; }
    public long CourseId { get; private set; }
    public string EnrollmentSourceCode { get; private set; } = string.Empty;
    public long EnrolledByLoginAccountId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public string EnrollmentStatusCode { get; private set; } = string.Empty;
}
