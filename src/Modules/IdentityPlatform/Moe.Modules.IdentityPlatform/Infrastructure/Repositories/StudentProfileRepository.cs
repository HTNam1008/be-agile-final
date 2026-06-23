using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class StudentProfileRepository(MoeDbContext dbContext) : IStudentProfileRepository
{
    public async Task<StudentProfileSummary?> GetProfileSummaryAsync(
        long personId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        Person? person = await dbContext.Set<Person>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        if (person is null)
        {
            return null;
        }

        var enrollment = await (
                from schoolEnrollment in dbContext.Set<SchoolEnrollment>().AsNoTracking()
                join organizationUnit in dbContext.Set<OrganizationUnit>().AsNoTracking()
                    on schoolEnrollment.OrganizationId equals organizationUnit.Id
                where schoolEnrollment.PersonId == personId
                    && schoolEnrollment.StartDate <= today
                    && (schoolEnrollment.EndDate == null || schoolEnrollment.EndDate >= today)
                orderby schoolEnrollment.SchoolingStatusCode == "ACTIVE" descending,
                    schoolEnrollment.StartDate descending
                select new
                {
                    schoolEnrollment.Id,
                    schoolEnrollment.OrganizationId,
                    organizationUnit.UnitCode,
                    organizationUnit.UnitName,
                    schoolEnrollment.StudentNumber,
                    schoolEnrollment.AcademicYear,
                    schoolEnrollment.LevelCode,
                    schoolEnrollment.ClassCode,
                    schoolEnrollment.SchoolingStatusCode,
                    schoolEnrollment.StartDate,
                    schoolEnrollment.EndDate
                })
            .FirstOrDefaultAsync(cancellationToken);

        return new StudentProfileSummary(
            person.Id,
            person.ExternalPersonReference,
            person.IdentityNumberMasked,
            person.OfficialFullName,
            person.DateOfBirth,
            person.NationalityCode,
            person.CitizenshipStatusCode,
            person.OfficialEmail,
            person.PreferredEmail,
            person.OfficialMobile,
            person.PreferredMobile,
            person.OfficialAddress,
            person.PreferredAddress,
            person.UpdatedAtUtc,
            enrollment?.Id,
            enrollment?.OrganizationId,
            enrollment?.UnitCode,
            enrollment?.UnitName,
            enrollment?.StudentNumber,
            enrollment?.AcademicYear,
            enrollment?.LevelCode,
            enrollment?.ClassCode,
            enrollment?.SchoolingStatusCode,
            enrollment?.StartDate,
            enrollment?.EndDate);
    }

    public async Task<UpdatePreferredContactResult> UpdatePreferredContactAsync(
        long personId,
        string? preferredEmail,
        string? preferredMobile,
        string? preferredAddress,
        DateTime? expectedUpdatedAtUtc,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        Person? person = await dbContext.Set<Person>()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        if (person is null)
        {
            return new UpdatePreferredContactResult(UpdatePreferredContactStatus.NotFound, null);
        }

        if (expectedUpdatedAtUtc is not null
            && TruncateToMilliseconds(person.UpdatedAtUtc) != TruncateToMilliseconds(expectedUpdatedAtUtc.Value))
        {
            return new UpdatePreferredContactResult(UpdatePreferredContactStatus.Conflict, null);
        }

        person.UpdatePreferredContact(preferredEmail, preferredMobile, preferredAddress, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        StudentProfileSummary? profile = await GetProfileSummaryAsync(personId, DateOnly.FromDateTime(utcNow), cancellationToken);
        return new UpdatePreferredContactResult(UpdatePreferredContactStatus.Updated, profile);
    }

    private static DateTime TruncateToMilliseconds(DateTime value)
    {
        return new DateTime(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
