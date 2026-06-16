using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Schooling;

internal sealed class SchoolEnrollment : Entity<long>
{
    private SchoolEnrollment() : base(0) { }

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
}
