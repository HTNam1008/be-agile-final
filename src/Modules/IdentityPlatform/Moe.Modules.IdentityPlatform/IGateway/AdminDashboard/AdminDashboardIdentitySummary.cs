namespace Moe.Modules.IdentityPlatform.IGateway.AdminDashboard;

public sealed record AdminDashboardIdentitySummary(
    long AdminUserAccountId,
    string DisplayName,
    long? OrganizationId,
    string? OrganizationName,
    long TotalSchools,
    long TotalActiveStudents);
