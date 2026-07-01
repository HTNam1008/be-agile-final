using Moe.SharedKernel.Domain;

namespace Moe.Modules.IdentityPlatform.Domain.Iam;

internal sealed class UserAccount : AggregateRoot<long>
{
    private UserAccount() : base(0) { }

    private UserAccount(
        long? personId,
        string identityProviderCode,
        string externalIssuer,
        string externalSubjectId,
        string? externalTenantId,
        string? externalObjectId,
        string? loginEmailNormalized,
        string? displayNameSnapshot,
        string userTypeCode,
        string portalAccessCode,
        string accountStatusCode,
        DateTime utcNow) : base(0)
    {
        PersonId = personId;
        IdentityProviderCode = identityProviderCode;
        ExternalIssuer = externalIssuer;
        ExternalSubjectId = externalSubjectId;
        ExternalTenantId = externalTenantId;
        ExternalObjectId = externalObjectId;
        LoginEmailNormalized = loginEmailNormalized;
        DisplayNameSnapshot = displayNameSnapshot;
        UserTypeCode = userTypeCode;
        PortalAccessCode = portalAccessCode;
        AccountStatusCode = accountStatusCode;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public long? PersonId { get; private set; }
    public long? AdminOrganizationId { get; private set; }
    public string RoleCode { get; private set; } = string.Empty;
    public string IdentityProviderCode { get; private set; } = string.Empty;
    public string? ExternalTenantId { get; private set; }
    public string ExternalIssuer { get; private set; } = string.Empty;
    public string ExternalSubjectId { get; private set; } = string.Empty;
    public string? ExternalObjectId { get; private set; }
    public string? ProviderDisplayName { get; private set; }
    public string? ProviderLoginName { get; private set; }
    public string? ProviderEmail { get; private set; }
    public string? ProviderMobile { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactMobile { get; private set; }
    public string? LoginEmailNormalized { get; private set; }
    public string? DisplayNameSnapshot { get; private set; }
    public string UserTypeCode { get; private set; } = string.Empty;
    public string PortalAccessCode { get; private set; } = string.Empty;
    public string AccountStatusCode { get; private set; } = string.Empty;
    public DateTime? FirstLoginAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public DateTime? LastSyncedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public long? CreatedByUserAccountId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActiveForLogin => AccountStatusCode is UserAccountStatusCodes.Active or UserAccountStatusCodes.PendingFirstLogin;

    public static UserAccount CreateAdmin(
        string externalIssuer,
        string externalSubjectId,
        string? externalTenantId,
        string? externalObjectId,
        string? email,
        string? displayName,
        string roleCode,
        long adminOrganizationId,
        long createdByUserAccountId,
        DateTime utcNow)
    {
        UserAccount account = new(
            null,
            IdentityProviderCodes.EntraWorkforce,
            externalIssuer,
            externalSubjectId,
            externalTenantId,
            externalObjectId,
            NormalizeEmail(email),
            displayName,
            UserTypeCodes.Internal,
            PortalAccessCodes.Admin,
            UserAccountStatusCodes.PendingFirstLogin,
            utcNow);

        account.RoleCode = roleCode;
        account.AdminOrganizationId = adminOrganizationId;
        account.CreatedByUserAccountId = createdByUserAccountId;
        return account;
    }

    public static UserAccount CreateBootstrapAdmin(
        string externalIssuer,
        string externalSubjectId,
        string? externalTenantId,
        string? externalObjectId,
        string? email,
        string? displayName,
        DateTime utcNow)
    {
        UserAccount account = new(
            null,
            IdentityProviderCodes.EntraWorkforce,
            externalIssuer,
            externalSubjectId,
            externalTenantId,
            externalObjectId,
            NormalizeEmail(email),
            displayName,
            UserTypeCodes.Internal,
            PortalAccessCodes.Admin,
            UserAccountStatusCodes.PendingFirstLogin,
            utcNow);

        account.RoleCode = RoleCodes.HqAdmin;
        account.AdminOrganizationId = OrganizationUnitCodes.MoeHeadquartersId;
        return account;
    }

    public static UserAccount CreateStudentSingpass(
        long personId,
        string externalIssuer,
        string externalSubjectId,
        string? displayName,
        long? createdByUserAccountId,
        DateTime utcNow)
    {
        UserAccount account = new(
            personId,
            IdentityProviderCodes.Singpass,
            externalIssuer,
            externalSubjectId,
            null,
            null,
            null,
            displayName,
            UserTypeCodes.EService,
            PortalAccessCodes.EService,
            UserAccountStatusCodes.PendingFirstLogin,
            utcNow);

        account.RoleCode = RoleCodes.Student;
        account.CreatedByUserAccountId = createdByUserAccountId;
        return account;
    }

    public void ActivateFirstLogin(DateTime utcNow)
    {
        if (AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin)
        {
            AccountStatusCode = UserAccountStatusCodes.Active;
            FirstLoginAtUtc = utcNow;
        }

        LastLoginAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void RecordSuccessfulLogin(DateTime utcNow)
    {
        if (AccountStatusCode == UserAccountStatusCodes.PendingFirstLogin)
        {
            AccountStatusCode = UserAccountStatusCodes.Active;
            FirstLoginAtUtc = utcNow;
        }

        LastLoginAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public void Disable(DateTime utcNow)
    {
        AccountStatusCode = UserAccountStatusCodes.Disabled;
        UpdatedAtUtc = utcNow;
    }

    public void Enable(DateTime utcNow)
    {
        AccountStatusCode = UserAccountStatusCodes.Active;
        UpdatedAtUtc = utcNow;
    }

    public void UpdateContactDetails(string? contactEmail, string? contactMobile, DateTime utcNow)
    {
        ContactEmail = NormalizeEmailForContact(contactEmail);
        ContactMobile = NormalizeNullable(contactMobile);
        UpdatedAtUtc = utcNow;
    }

    private static string? NormalizeEmailForContact(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToUpperInvariant();
    }
}
