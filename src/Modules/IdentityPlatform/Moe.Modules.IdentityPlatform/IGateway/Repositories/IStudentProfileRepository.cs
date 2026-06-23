namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IStudentProfileRepository
{
    Task<StudentProfileSummary?> GetProfileSummaryAsync(long personId, DateOnly today, CancellationToken cancellationToken);

    Task<UpdatePreferredContactResult> UpdatePreferredContactAsync(
        long personId,
        string? preferredEmail,
        string? preferredMobile,
        string? preferredAddress,
        DateTime? expectedUpdatedAtUtc,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

internal sealed record UpdatePreferredContactResult(
    UpdatePreferredContactStatus Status,
    StudentProfileSummary? Profile);

internal enum UpdatePreferredContactStatus
{
    Updated,
    NotFound,
    Conflict
}
