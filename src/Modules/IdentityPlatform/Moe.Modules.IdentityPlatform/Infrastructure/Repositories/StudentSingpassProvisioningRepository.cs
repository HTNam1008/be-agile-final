using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class StudentSingpassProvisioningRepository(MoeDbContext dbContext) : IStudentSingpassProvisioningRepository
{
    public Task<Person?> FindPersonAsync(long personId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Person>()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);
    }

    public Task<UserAccount?> FindSingpassAccountForRequestAsync(
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<UserAccount>()
            .SingleOrDefaultAsync(x => x.PersonId == request.PersonId
                && x.IdentityProviderCode == IdentityProviderCodes.Singpass
                && x.ExternalIssuer == request.ExternalIssuer
                && x.ExternalSubjectId == request.ExternalSubjectId,
                cancellationToken);
    }

    public async Task AddAccountAndRequestAsync(
        UserAccount account,
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<UserAccount>().AddAsync(account, cancellationToken);
        await dbContext.Set<IdentityProvisioningRequest>().AddAsync(request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureActiveStudentScopeAsync(
        long userAccountId,
        long actorUserAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        bool hasStudentScope = await dbContext.Set<UserAccessScope>()
            .AnyAsync(x => x.UserAccountId == userAccountId
                && x.OrganizationUnitId == OrganizationUnitCodes.MoeHeadquartersId
                && x.RoleCode == RoleCodes.Student
                && x.StatusCode == IamStatusCodes.Active
                && x.EffectiveFromUtc <= utcNow
                && (x.EffectiveToUtc == null || x.EffectiveToUtc > utcNow),
                cancellationToken);

        if (hasStudentScope)
        {
            return;
        }

        UserAccessScope studentScope = new(
            userAccountId,
            OrganizationUnitCodes.MoeHeadquartersId,
            RoleCodes.Student,
            actorUserAccountId,
            utcNow,
            utcNow);

        await dbContext.Set<UserAccessScope>().AddAsync(studentScope, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
