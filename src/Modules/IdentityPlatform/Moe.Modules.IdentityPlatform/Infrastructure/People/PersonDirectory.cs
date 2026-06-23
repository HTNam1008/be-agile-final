using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

public sealed class PersonDirectory(MoeDbContext db) : IPersonDirectory
{
    public Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken)
        => db.Set<Person>().AsNoTracking().Where(x => x.Id == personId)
            .Select(x => new PersonSummary(x.Id, x.OfficialFullName, x.DateOfBirth, x.NationalityCode, x.CitizenshipStatusCode))
            .SingleOrDefaultAsync(cancellationToken);
}
