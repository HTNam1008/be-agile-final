namespace Moe.Modules.IdentityPlatform.IGateway.Students;

public interface IStudentDirectory
{
    Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken);
}
