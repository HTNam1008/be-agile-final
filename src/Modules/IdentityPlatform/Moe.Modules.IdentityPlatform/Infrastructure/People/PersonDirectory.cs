using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

public sealed class PersonDirectory(MoeDbContext db, IClock clock) : IPersonDirectory
{
    public Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        return db.Set<Person>().AsNoTracking()
            .Where(x => x.Id == personId)
            .Select(x => new PersonSummary(
                x.Id,
                x.OfficialFullName,
                x.DateOfBirth,
                x.NationalityCode,
                x.CitizenshipStatusCode,
                db.Set<SchoolEnrollment>()
                    .AsNoTracking()
                    .Where(e => e.PersonId == x.Id
                        && e.SchoolingStatusCode == "ACTIVE"
                        && e.StartDate <= today
                        && (e.EndDate == null || e.EndDate >= today))
                    .OrderByDescending(e => e.StartDate)
                    .ThenByDescending(e => e.Id)
                    .Select(e => (long?)e.OrganizationId)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
