using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class StudentDirectory(MoeDbContext dbContext) : IStudentDirectory
{
    public async Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
    {
        var student = await (
                from person in dbContext.Set<Person>().AsNoTracking()
                where person.Id == personId
                join schoolEnrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
                    on person.Id equals schoolEnrollment.PersonId into schoolEnrollments
                from schoolEnrollment in schoolEnrollments
                    .Where(x => x.SchoolingStatusCode == "ACTIVE")
                    .OrderByDescending(x => x.StartDate)
                    .Take(1)
                    .DefaultIfEmpty()
                join organization in dbContext.Set<OrganizationUnit>().AsNoTracking()
                    on schoolEnrollment.OrganizationId equals organization.Id into organizations
                from organization in organizations.DefaultIfEmpty()
                select new
                {
                    person.Id,
                    person.OfficialFullName,
                    person.DateOfBirth,
                    SchoolName = organization == null ? null : organization.UnitName
                })
            .SingleOrDefaultAsync(cancellationToken);

        return student is null
            ? null
            : new StudentSummary(
                student.Id,
                student.OfficialFullName,
                student.DateOfBirth,
                IsAccountHolder: true,
                student.SchoolName);
    }
}
