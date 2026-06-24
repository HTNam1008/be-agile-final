using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class StudentOnboardingRepository(MoeDbContext dbContext) : IStudentOnboardingRepository
{
    public Task<bool> IdentityNumberExistsAsync(byte[] identityNumberHash, CancellationToken cancellationToken)
    {
        return dbContext.Set<PersonIdentifier>()
            .AnyAsync(x => x.IdentifierTypeCode == "IDENTITY_NUMBER"
                && x.IdentifierStatusCode == PersonIdentifierStatusCodes.Active
                && x.IdentifierValueHash.SequenceEqual(identityNumberHash),
                cancellationToken);
    }

    public Task<bool> StudentNumberExistsAsync(string studentNumber, CancellationToken cancellationToken)
    {
        string normalizedStudentNumber = studentNumber.Trim().ToUpperInvariant();

        return dbContext.Set<SchoolEnrollment>()
            .AnyAsync(x => x.StudentNumber == normalizedStudentNumber, cancellationToken);
    }

    public async Task<long> AddPersonAsync(
        Person person,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        await dbContext.Set<Person>().AddAsync(person, cancellationToken);
        if (saveChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return person.Id;
    }

    public async Task<CreatedStudentRecord> AddStudentIdentityAndEnrollmentAsync(
        PersonIdentifier identityNumber,
        SchoolEnrollment enrollment,
        CancellationToken cancellationToken,
        bool saveChanges = true)
    {
        await dbContext.Set<PersonIdentifier>().AddAsync(identityNumber, cancellationToken);
        await dbContext.Set<SchoolEnrollment>().AddAsync(enrollment, cancellationToken);
        if (saveChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new CreatedStudentRecord(enrollment.PersonId, enrollment.Id);
    }
}
