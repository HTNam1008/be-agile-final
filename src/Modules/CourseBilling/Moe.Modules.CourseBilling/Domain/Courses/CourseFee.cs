using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseFee : Entity<long>
{
    private CourseFee() : base(0) { }

    public long CourseId { get; private set; }
    public long FeeComponentId { get; private set; }
    public decimal FeeValue { get; private set; }
    public int SequenceNumber { get; private set; }
    public bool IsActive { get; private set; }
}
