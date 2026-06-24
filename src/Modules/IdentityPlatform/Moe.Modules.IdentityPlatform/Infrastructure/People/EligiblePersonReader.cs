using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

internal sealed class EligiblePersonReader(MoeDbContext dbContext) : IEligiblePersonReader
{
    public async Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
        DateOnly today,
        CancellationToken cancellationToken)
    {
        DateOnly oldestEligibleBirthDate = today.AddYears(-31).AddDays(1);
        DateOnly youngestEligibleBirthDate = today.AddYears(-16);

        return await dbContext.Set<Person>()
            .AsNoTracking()
            .Where(person =>
                person.DateOfBirth >= oldestEligibleBirthDate &&
                person.DateOfBirth <= youngestEligibleBirthDate &&
                person.CitizenshipStatusCode == ResidencyStatusCodes.Citizen &&
                person.PersonStatusCode == PersonStatusCodes.Active)
            .Select(person => person.Id)
            .ToArrayAsync(cancellationToken);
    }
}
