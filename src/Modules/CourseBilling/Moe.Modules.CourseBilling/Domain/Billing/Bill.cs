using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Billing;

internal sealed class Bill : Entity<long>
{
    private Bill() : base(0) { }

    public string BillNumber { get; private set; } = string.Empty;
    public long CourseEnrollmentId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal SubsidyAmount { get; private set; }
    public decimal NetPayableAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public string BillStatusCode { get; private set; } = string.Empty;
}
