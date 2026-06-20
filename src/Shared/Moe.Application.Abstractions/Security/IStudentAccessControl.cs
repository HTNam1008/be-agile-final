namespace Moe.Application.Abstractions.Security;

public interface IStudentAccessControl
{
    long? PersonId { get; }
    bool IsStudent { get; }
    bool CanAccessOwnPerson(long personId);
    Task<bool> CanUseSchoolServiceAsync(long organizationId, CancellationToken cancellationToken);
}
