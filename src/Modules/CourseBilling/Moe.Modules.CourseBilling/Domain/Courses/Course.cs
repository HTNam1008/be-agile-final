using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class Course : Entity<long>
{
    private Course() : base(0) { }

    public Course(
        string courseCode,
        string courseName,
        string? description,

        DateOnly startDate,
        DateOnly endDate,
        DateTime enrollmentCloseAtUtc,
        DateTime utcNow) : base(0)
    {
        OrganizationId = 0;
        CourseCode = courseCode.Trim();
        CourseName = courseName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        EnrollmentOpenAtUtc = utcNow;
        EnrollmentCloseAtUtc = enrollmentCloseAtUtc;
        CourseStatusCode = CourseStatusCodes.Draft;
        CreatedByLoginAccountId = 0;
        UpdatedByLoginAccountId = 0;
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
        DateTime enrollmentCloseAtUtc,
        DateTime utcNow)
    {
        CourseCode = courseCode.Trim();
        CourseName = courseName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        EnrollmentCloseAtUtc = enrollmentCloseAtUtc;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = 0;
    }

    public void Disable(DateTime utcNow)
    {
        CourseStatusCode = CourseStatusCodes.Disabled;
        DisabledAtUtc = utcNow;
        DisabledByLoginAccountId = null;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = 0;
    }

    public void Publish(DateTime utcNow)
    {
        CourseStatusCode = CourseStatusCodes.Published;
        DisabledAtUtc = null;
        DisabledByLoginAccountId = null;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = 0;
    }

    public void Enable(DateTime utcNow)
    {
        Publish(utcNow);
    }
}
