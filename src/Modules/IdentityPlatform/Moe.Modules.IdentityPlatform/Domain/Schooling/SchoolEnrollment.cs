using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Schooling;

public class SchoolEnrollment : Entity<long>
{
    private SchoolEnrollment() : base(0) { }

    public SchoolEnrollment(
        long personId,
        long organizationId,
        string studentNumber,
        string academicYear,
        string levelCode,
        string classCode,
        DateOnly startDate,
        DateTime utcNow) : base(0)
    {
        PersonId = personId;
        OrganizationId = organizationId;
        StudentNumber = studentNumber.Trim().ToUpperInvariant();
        AcademicYear = academicYear.Trim();
        LevelCode = levelCode.Trim().ToUpperInvariant();
        ClassCode = classCode.Trim().ToUpperInvariant();
        SchoolingStatusCode = "ACTIVE";
        StartDate = startDate;
        SourceCode = "ADMIN_MANUAL";
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public long PersonId { get; private set; }
    public long OrganizationId { get; private set; }
    public string StudentNumber { get; private set; } = string.Empty;
    public string AcademicYear { get; private set; } = string.Empty;
    public string LevelCode { get; private set; } = string.Empty;
    public string ClassCode { get; private set; } = string.Empty;
    public string SchoolingStatusCode { get; private set; } = string.Empty;
    public string? StatusReasonCode { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string SourceCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateClassCode(string classCode, DateTime utcNow)
    {
        ClassCode = classCode.Trim().ToUpperInvariant();
        UpdatedAtUtc = utcNow;
    }
}
