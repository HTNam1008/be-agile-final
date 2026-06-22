namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IAdminAccountDetailsRepository
{
    Task<AdminAccountDetailsProfile?> GetAsync(long personId, DateOnly today, CancellationToken cancellationToken);

    Task<AdminAccountDetailsUpdateResult> UpdateAsync(
        long personId,
        string? classCode,
        string? preferredAddress,
        string? preferredEmail,
        string? preferredMobile,
        DateTime? expectedUpdatedAtUtc,
        DateTime utcNow,
        DateOnly today,
        CancellationToken cancellationToken);
}

internal sealed record AdminAccountDetailsUpdateResult(
    AdminAccountDetailsUpdateStatus Status,
    AdminAccountDetailsProfile? Profile,
    IReadOnlyCollection<string> ChangedFields)
{
    public static AdminAccountDetailsUpdateResult Updated(
        AdminAccountDetailsProfile profile,
        IReadOnlyCollection<string> changedFields)
        => new(AdminAccountDetailsUpdateStatus.Updated, profile, changedFields);

    public static AdminAccountDetailsUpdateResult NotFound()
        => new(AdminAccountDetailsUpdateStatus.NotFound, null, []);

    public static AdminAccountDetailsUpdateResult Conflict()
        => new(AdminAccountDetailsUpdateStatus.Conflict, null, []);

    public static AdminAccountDetailsUpdateResult ClassEnrollmentMissing()
        => new(AdminAccountDetailsUpdateStatus.ClassEnrollmentMissing, null, []);
}

internal enum AdminAccountDetailsUpdateStatus
{
    Updated,
    NotFound,
    Conflict,
    ClassEnrollmentMissing
}
