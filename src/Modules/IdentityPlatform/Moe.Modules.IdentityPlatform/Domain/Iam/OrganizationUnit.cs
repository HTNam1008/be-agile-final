using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class OrganizationUnit : AggregateRoot<long>
{
    private OrganizationUnit() : base(0) { }

    public OrganizationUnit(string unitCode, string unitName, string unitTypeCode, DateTime effectiveFromUtc) : base(0)
    {
        UnitCode = unitCode;
        UnitName = unitName;
        UnitTypeCode = unitTypeCode;
        StatusCode = IamStatusCodes.Active;
        EffectiveFromUtc = effectiveFromUtc;
    }

    public long? ParentOrganizationUnitId { get; private set; }
    public string UnitCode { get; private set; } = string.Empty;
    public string UnitName { get; private set; } = string.Empty;
    public string UnitTypeCode { get; private set; } = string.Empty;
    public string? MockPassSchoolCode { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
