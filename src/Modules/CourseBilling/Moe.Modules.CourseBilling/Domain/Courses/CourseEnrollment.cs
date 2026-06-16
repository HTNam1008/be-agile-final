using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseEnrollment : Entity<long>
{
    private CourseEnrollment() : base(0) { }

    public long PersonId { get; private set; }
    public long CourseId { get; private set; }
    public string EnrollmentSourceCode { get; private set; } = string.Empty;
    public long? EnrolledByLoginAccountId { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public string EnrollmentStatusCode { get; private set; } = string.Empty;
    public DateTime? ExitAtUtc { get; private set; }
    public string? ExitReasonCode { get; private set; }
}
