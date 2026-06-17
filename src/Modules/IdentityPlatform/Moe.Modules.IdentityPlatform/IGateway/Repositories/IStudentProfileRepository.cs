namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal interface IStudentProfileRepository
{
    Task<StudentProfileSummary?> GetProfileSummaryAsync(long personId, DateOnly today, CancellationToken cancellationToken);
}
