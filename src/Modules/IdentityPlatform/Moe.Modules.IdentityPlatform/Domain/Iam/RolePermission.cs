using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class RolePermission : Entity<long>
{
    private RolePermission() : base(0) { }

    public RolePermission(string roleCode, string permissionCode, DateTime effectiveFromUtc) : base(0)
    {
        RoleCode = roleCode;
        PermissionCode = permissionCode;
        StatusCode = IamStatusCodes.Active;
        EffectiveFromUtc = effectiveFromUtc;
    }

    public string RoleCode { get; private set; } = string.Empty;
    public string PermissionCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
