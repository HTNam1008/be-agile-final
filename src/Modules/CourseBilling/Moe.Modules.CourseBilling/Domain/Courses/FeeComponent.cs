using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class FeeComponent : Entity<long>
{
    private FeeComponent() : base(0) { }

    public string ComponentCode { get; private set; } = string.Empty;
    public string ComponentName { get; private set; } = string.Empty;
    public string ComponentTypeCode { get; private set; } = string.Empty;
    public string CalculationTypeCode { get; private set; } = string.Empty;
    public bool IsTaxComponent { get; private set; }
    public bool IsActive { get; private set; }
}
