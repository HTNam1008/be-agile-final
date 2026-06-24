using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;

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
        DateTime utcNow,
        decimal beforeStartRefundPercentage = CourseRefundPolicyDefaults.BeforeStartPercentage,
        decimal afterStartRefundPercentage = CourseRefundPolicyDefaults.AfterStartPercentage) : base(0)
    {
        EnsureValidRefundPercentage(beforeStartRefundPercentage, nameof(beforeStartRefundPercentage));
        EnsureValidRefundPercentage(afterStartRefundPercentage, nameof(afterStartRefundPercentage));

        OrganizationId = organizationId;
        CourseCode = courseCode.Trim();
        CourseName = courseName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        EndDate = endDate;
        EnrollmentOpenAtUtc = enrollmentOpenAtUtc;
        EnrollmentCloseAtUtc = enrollmentCloseAtUtc;
        BeforeStartRefundPercentage = beforeStartRefundPercentage;
        AfterStartRefundPercentage = afterStartRefundPercentage;
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
    public decimal BeforeStartRefundPercentage { get; private set; }
    public decimal AfterStartRefundPercentage { get; private set; }
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

    public Result UpdateRefundPolicy(
        decimal beforeStartRefundPercentage,
        decimal afterStartRefundPercentage,
        long actorLoginAccountId,
        DateTime utcNow)
    {
        if (!IsValidRefundPercentage(beforeStartRefundPercentage) ||
            !IsValidRefundPercentage(afterStartRefundPercentage))
        {
            return Result.Failure(CourseErrors.InvalidRefundPercentage);
        }

        BeforeStartRefundPercentage = beforeStartRefundPercentage;
        AfterStartRefundPercentage = afterStartRefundPercentage;
        UpdatedAtUtc = utcNow;
        UpdatedByLoginAccountId = actorLoginAccountId;
        return Result.Success();
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

    private static bool IsValidRefundPercentage(decimal percentage)
        => percentage is >= 0m and <= 100m;

    private static void EnsureValidRefundPercentage(decimal percentage, string parameterName)
    {
        if (!IsValidRefundPercentage(percentage))
            throw new ArgumentOutOfRangeException(parameterName, percentage, "Refund percentage must be between 0 and 100.");
    }
}

internal static class CourseRefundPolicyDefaults
{
    public const decimal BeforeStartPercentage = 100m;
    public const decimal AfterStartPercentage = 50m;
}
