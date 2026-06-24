using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;

namespace Moe.Modules.IdentityPlatform.IGateway.Repositories;

internal sealed record CreatedStudentRecord(
    long PersonId,
    long SchoolEnrollmentId);

internal interface IStudentOnboardingRepository
{
    Task<bool> IdentityNumberExistsAsync(byte[] identityNumberHash, CancellationToken cancellationToken);

    Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken);

    Task<long> AddPersonAsync(
        Person person,
        CancellationToken cancellationToken,
        bool saveChanges = true);

    Task<CreatedStudentRecord> AddStudentIdentityAndEnrollmentAsync(
        PersonIdentifier identityNumber,
        SchoolEnrollment enrollment,
        CancellationToken cancellationToken,
        bool saveChanges = true);
}
