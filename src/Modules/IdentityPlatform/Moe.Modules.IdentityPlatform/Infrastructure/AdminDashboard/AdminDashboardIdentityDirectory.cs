using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.AdminDashboard;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.AdminDashboard;

internal sealed class AdminDashboardIdentityDirectory(MoeDbContext dbContext) : IAdminDashboardIdentityDirectory
{
    public async Task<AdminDashboardIdentitySummary?> GetSummaryAsync(
        long userAccountId,
        long? organizationId,
        CancellationToken cancellationToken)
    {
        var admin = await (
            from account in dbContext.Set<UserAccount>().AsNoTracking()
            join organization in dbContext.Set<OrganizationUnit>().AsNoTracking()
                on organizationId equals organization.Id into organizations
            from organization in organizations.DefaultIfEmpty()
            where account.Id == userAccountId
            select new
            {
                account.Id,
                DisplayName = account.DisplayNameSnapshot ?? account.ProviderDisplayName ?? string.Empty,
                OrganizationName = organization == null ? null : organization.UnitName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return null;
        }

        DateTime utcNow = DateTime.UtcNow;
        long totalSchools = await dbContext.Set<OrganizationUnit>()
            .AsNoTracking()
            .Where(organization => organization.UnitTypeCode == "SCHOOL"
                && organization.StatusCode == "ACTIVE"
                && organization.EffectiveFromUtc <= utcNow
                && (organization.EffectiveToUtc == null || organization.EffectiveToUtc > utcNow)
                && (organizationId == null || organization.Id == organizationId))
            .LongCountAsync(cancellationToken);

        long totalActiveStudents = await (
            from enrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
            join person in dbContext.Set<Person>().AsNoTracking()
                on enrollment.PersonId equals person.Id
            where (organizationId == null || enrollment.OrganizationId == organizationId)
                && enrollment.SchoolingStatusCode == "ACTIVE"
                && person.PersonStatusCode == "ACTIVE"
            select enrollment.PersonId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        return new AdminDashboardIdentitySummary(
            admin.Id,
            admin.DisplayName,
            organizationId,
            organizationId is null ? "Whole system" : admin.OrganizationName,
            totalSchools,
            totalActiveStudents);
    }
}
