using Moe.SharedKernel.Domain;

namespace Moe.Modules.CourseBilling.Domain.Courses;

internal sealed class FeeComponent : Entity<long>
{
    private FeeComponent() : base(0) { }

    public FeeComponent(
        string componentCode,
        string componentName,
        string componentTypeCode,
        string calculationTypeCode,
        bool isTaxComponent,
        bool isActive) : base(0)
    {
        ComponentCode = componentCode.Trim();
        ComponentName = componentName.Trim();
        ComponentTypeCode = componentTypeCode.Trim();
        CalculationTypeCode = calculationTypeCode.Trim();
        IsTaxComponent = isTaxComponent;
        IsActive = isActive;
    }

    public string ComponentCode { get; private set; } = string.Empty;
    public string ComponentName { get; private set; } = string.Empty;
    public string ComponentTypeCode { get; private set; } = string.Empty;
    public string CalculationTypeCode { get; private set; } = string.Empty;
    public bool IsTaxComponent { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(
        string componentCode,
        string componentName,
        string componentTypeCode,
        string calculationTypeCode,
        bool isTaxComponent,
        bool isActive)
    {
        ComponentCode = componentCode.Trim();
        ComponentName = componentName.Trim();
        ComponentTypeCode = componentTypeCode.Trim();
        CalculationTypeCode = calculationTypeCode.Trim();
        IsTaxComponent = isTaxComponent;
        IsActive = isActive;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
