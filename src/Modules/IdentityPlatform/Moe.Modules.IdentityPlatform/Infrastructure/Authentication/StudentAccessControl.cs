using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Authentication;

internal sealed class StudentAccessControl(
    ICurrentUser currentUser,
    MoeDbContext dbContext,
    IClock clock) : IStudentAccessControl
{
    public long? PersonId => currentUser.PersonId;

    public bool IsStudent => currentUser.Portal == PortalAccessCodes.EService
        && currentUser.Roles.Contains(RoleCodes.Student);

    public bool CanAccessOwnPerson(long personId)
    {
        return IsStudent && currentUser.PersonId == personId;
    }

    public async Task<bool> CanUseSchoolServiceAsync(long organizationId, CancellationToken cancellationToken)
    {
        if (!IsStudent || currentUser.PersonId is not long personId || organizationId <= 0)
        {
            return false;
        }

        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

        return await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .AnyAsync(x => x.PersonId == personId
                && x.OrganizationId == organizationId
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= today
                && (x.EndDate == null || x.EndDate >= today),
                cancellationToken);
    }
}
