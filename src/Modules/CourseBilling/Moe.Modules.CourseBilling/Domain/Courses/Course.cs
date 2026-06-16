using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class Course : Entity<long>
{
    private Course() : base(0) { }

    public long OrganizationId { get; private set; }
    public string CourseCode { get; private set; } = string.Empty;
    public string CourseName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string AcademicYear { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public DateTime? EnrollmentOpenAtUtc { get; private set; }
    public DateTime? EnrollmentCloseAtUtc { get; private set; }
    public string CourseStatusCode { get; private set; } = string.Empty;
    public long CreatedByLoginAccountId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long? UpdatedByLoginAccountId { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public long? DisabledByLoginAccountId { get; private set; }
    public DateTime? DisabledAtUtc { get; private set; }
    public string? DisabledReason { get; private set; }
}
