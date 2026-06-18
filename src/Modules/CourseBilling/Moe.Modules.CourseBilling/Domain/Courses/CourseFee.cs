using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class CourseFee : Entity<long>
{
    private CourseFee() : base(0) { }

    public CourseFee(long courseId, long feeComponentId, decimal feeValue, int sequenceNumber) : base(0)
    {
        CourseId = courseId;
        FeeComponentId = feeComponentId;
        FeeValue = feeValue;
        SequenceNumber = sequenceNumber;
        IsActive = true;
    }

    public long CourseId { get; private set; }
    public long FeeComponentId { get; private set; }
    public decimal FeeValue { get; private set; }
    public int SequenceNumber { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(decimal feeValue, int sequenceNumber)
    {
        FeeValue = feeValue;
        SequenceNumber = sequenceNumber;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
