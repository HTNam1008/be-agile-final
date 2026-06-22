using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class Course : Entity<long>
{
    private Course() : base(0) { }

    public Course(
        long organizationId,
        string courseCode,
        string courseName,
        string? description,

        DateOnly startDate,
        DateOnly endDate,
        DateTime enrollmentOpenAtUtc,
        DateTime enrollmentCloseAtUtc,
        long actorLoginAccountId,
        DateTime utcNow) : base(0)
    {
        OrganizationId = organizationId;
        CourseCode = courseCode.Trim();
        CourseName = courseName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        EnrollmentOpenAtUtc = enrollmentOpenAtUtc;
        EnrollmentCloseAtUtc = enrollmentCloseAtUtc;
        CourseStatusCode = CourseStatusCodes.Draft;
        CreatedByLoginAccountId = actorLoginAccountId;
        UpdatedByLoginAccountId = actorLoginAccountId;
        UpdatedAtUtc = utcNow;
    }

    public long OrganizationId { get; private set; }
    public string CourseCode { get; private set; } = string.Empty;
    public string CourseName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateTime EnrollmentOpenAtUtc { get; private set; }
    public DateTime EnrollmentCloseAtUtc { get; private set; }
    public string CourseStatusCode { get; private set; } = string.Empty;
    public long CreatedByLoginAccountId { get; private set; }
    public long UpdatedByLoginAccountId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public long? DisabledByLoginAccountId { get; private set; }
    public DateTime? DisabledAtUtc { get; private set; }

    public bool IsDisabled => CourseStatusCode == CourseStatusCodes.Disabled;
    public bool IsDraft => CourseStatusCode == CourseStatusCodes.Draft;
    public bool IsPublished => CourseStatusCode == CourseStatusCodes.Published;

    public void Update(
        string courseCode,
        string courseName,
        string? description,
        DateOnly startDate,
        DateOnly endDate,
        DateTime enrollmentOpenAtUtc,
        DateTime enrollmentCloseAtUtc,
        long actorLoginAccountId,
        DateTime utcNow)
    {
        CourseCode = courseCode.Trim();
        CourseName = courseName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        EnrollmentOpenAtUtc = enrollmentOpenAtUtc;
        EnrollmentCloseAtUtc = enrollmentCloseAtUtc;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = actorLoginAccountId;
    }

    public void Disable(long actorLoginAccountId, DateTime utcNow)
    {
        CourseStatusCode = CourseStatusCodes.Disabled;
        DisabledAtUtc = utcNow;
        DisabledByLoginAccountId = actorLoginAccountId;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = actorLoginAccountId;
    }

    public void Publish(long actorLoginAccountId, DateTime utcNow)
    {
        CourseStatusCode = CourseStatusCodes.Published;
        DisabledAtUtc = null;
        DisabledByLoginAccountId = null;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = actorLoginAccountId;
    }

    public void Enable(long actorLoginAccountId, DateTime utcNow)
    {
        Publish(actorLoginAccountId, utcNow);
    }
}
