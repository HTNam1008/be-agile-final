using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class UserAccessScope : AggregateRoot<long>
{
    private UserAccessScope() : base(0) { }

    public UserAccessScope(
        long userAccountId,
        long organizationUnitId,
        string roleCode,
        long createdByUserAccountId,
        DateTime effectiveFromUtc,
        DateTime createdAtUtc) : base(0)
    {
        UserAccountId = userAccountId;
        OrganizationUnitId = organizationUnitId;
        RoleCode = roleCode;
        StatusCode = IamStatusCodes.Active;
        CreatedByUserAccountId = createdByUserAccountId;
        EffectiveFromUtc = effectiveFromUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public long UserAccountId { get; private set; }
    public long OrganizationUnitId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long? CreatedByUserAccountId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsEffective(DateTime utcNow)
    {
        return StatusCode == IamStatusCodes.Active && EffectiveFromUtc <= utcNow && (EffectiveToUtc is null || EffectiveToUtc > utcNow);
    }

    public void Revoke(DateTime utcNow)
    {
        StatusCode = IamStatusCodes.Revoked;
        EffectiveToUtc = utcNow;
    }
}
