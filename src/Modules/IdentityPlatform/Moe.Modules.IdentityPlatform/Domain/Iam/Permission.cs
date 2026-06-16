namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class Permission
{
    private Permission() { }

    public Permission(
        string permissionCode,
        string permissionName,
        string moduleCode,
        string actionCode,
        string resourceCode)
    {
        PermissionCode = permissionCode;
        PermissionName = permissionName;
        ModuleCode = moduleCode;
        ActionCode = actionCode;
        ResourceCode = resourceCode;
        StatusCode = IamStatusCodes.Active;
    }

    public string PermissionCode { get; private set; } = string.Empty;
    public string PermissionName { get; private set; } = string.Empty;
    public string ModuleCode { get; private set; } = string.Empty;
    public string ActionCode { get; private set; } = string.Empty;
    public string ResourceCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
}
