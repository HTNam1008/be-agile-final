using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.People;

public sealed class PersonLifecycleGateway(MoeDbContext db) : IPersonLifecycleGateway
{
    public async Task<Result> DisableAsync(long personId, DateTime utcNow, CancellationToken cancellationToken)
    {
        Person? person = await db.Set<Person>()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        if (person is null)
        {
            return Result.Failure(new Error(
                "IDENTITY.PERSON_NOT_FOUND",
                "The person was not found."));
        }

        person.Disable(utcNow);
        return Result.Success();
    }

    public async Task<Result> EnableAsync(long personId, DateTime utcNow, CancellationToken cancellationToken)
    {
        Person? person = await db.Set<Person>()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        if (person is null)
        {
            return Result.Failure(new Error(
                "IDENTITY.PERSON_NOT_FOUND",
                "The person was not found."));
        }

        person.Enable(utcNow);
        return Result.Success();
    }
}
