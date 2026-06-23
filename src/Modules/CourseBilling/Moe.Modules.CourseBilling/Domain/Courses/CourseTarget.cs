using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseTarget : Entity<long>
{
    private CourseTarget() : base(0) { }

    public long CourseId { get; private set; }
    public string TargetTypeCode { get; private set; } = string.Empty;
    public string? LevelCode { get; private set; }
    public string? ClassCode { get; private set; }
}
