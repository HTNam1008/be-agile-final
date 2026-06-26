using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Repositories;

internal sealed class AdminAccountDetailsRepository(MoeDbContext dbContext) : IAdminAccountDetailsRepository
{
    public async Task<AdminAccountDetailsProfile?> GetAsync(
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

        EnrollmentProjection? enrollment = await GetCurrentEnrollmentQuery(personId, today)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        string? identifierMasked = await dbContext.Set<PersonIdentifier>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId
                && x.IdentifierTypeCode == "IDENTITY_NUMBER"
                && x.IdentifierStatusCode == PersonIdentifierStatusCodes.Active
                && x.IsPrimary)
            .Select(x => x.IdentifierMasked)
            .SingleOrDefaultAsync(cancellationToken);

        return ToProfile(person, enrollment, identifierMasked ?? person.IdentityNumberMasked);
    }

    public async Task<AdminAccountDetailsUpdateResult> UpdateAsync(
        long personId,
        string? classCode,
        string? preferredAddress,
        string? preferredEmail,
        string? preferredMobile,
        DateTime? expectedUpdatedAtUtc,
        DateTime utcNow,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        Person? person = await dbContext.Set<Person>()
            .SingleOrDefaultAsync(x => x.Id == personId, cancellationToken);

        if (person is null)
        {
            return AdminAccountDetailsUpdateResult.NotFound();
        }

        EnrollmentProjection? enrollmentProjection = await GetCurrentEnrollmentQuery(personId, today)
            .FirstOrDefaultAsync(cancellationToken);

        DateTime currentToken = CalculateUpdatedAtToken(person.UpdatedAtUtc, enrollmentProjection?.UpdatedAtUtc);
        if (expectedUpdatedAtUtc is not null
            && TruncateToMilliseconds(currentToken) != TruncateToMilliseconds(expectedUpdatedAtUtc.Value))
        {
            return AdminAccountDetailsUpdateResult.Conflict();
        }

        if (!string.IsNullOrWhiteSpace(classCode) && enrollmentProjection is null)
        {
            return AdminAccountDetailsUpdateResult.ClassEnrollmentMissing();
        }

        List<string> changedFields = [];
        string? normalizedEmail = NormalizeNullable(preferredEmail);
        string? normalizedMobile = NormalizeNullable(preferredMobile);
        string? normalizedAddress = NormalizeNullable(preferredAddress);

        if (NormalizeNullable(person.PreferredEmail) != normalizedEmail)
        {
            changedFields.Add("email");
        }

        if (NormalizeNullable(person.PreferredMobile) != normalizedMobile)
        {
            changedFields.Add("contactNumber");
        }

        if (NormalizeNullable(person.PreferredAddress) != normalizedAddress)
        {
            changedFields.Add("residentialAddress");
        }

        person.UpdatePreferredContact(preferredEmail, preferredMobile, preferredAddress, utcNow);

        SchoolEnrollment? enrollment = null;
        if (enrollmentProjection is not null && !string.IsNullOrWhiteSpace(classCode))
        {
            enrollment = await dbContext.Set<SchoolEnrollment>()
                .SingleAsync(x => x.Id == enrollmentProjection.Id, cancellationToken);

            string normalizedClassCode = classCode.Trim().ToUpperInvariant();
            if (enrollment.ClassCode != normalizedClassCode)
            {
                changedFields.Add("classCode");
                enrollment.UpdateClassCode(classCode, utcNow);
            }
        }

        string? identifierMasked = await dbContext.Set<PersonIdentifier>()
            .AsNoTracking()
            .Where(x => x.PersonId == personId
                && x.IdentifierTypeCode == "IDENTITY_NUMBER"
                && x.IdentifierStatusCode == PersonIdentifierStatusCodes.Active
                && x.IsPrimary)
            .Select(x => x.IdentifierMasked)
            .SingleOrDefaultAsync(cancellationToken);

        EnrollmentProjection? updatedEnrollment = enrollmentProjection;
        if (enrollmentProjection is not null && enrollment is not null)
        {
            updatedEnrollment = enrollmentProjection with
            {
                ClassCode = enrollment.ClassCode,
                UpdatedAtUtc = enrollment.UpdatedAtUtc
            };
        }

        AdminAccountDetailsProfile profile = ToProfile(person, updatedEnrollment, identifierMasked ?? person.IdentityNumberMasked);
        return AdminAccountDetailsUpdateResult.Updated(profile, changedFields);
    }

    private IQueryable<EnrollmentProjection> GetCurrentEnrollmentQuery(long personId, DateOnly today)
        => from schoolEnrollment in dbContext.Set<SchoolEnrollment>()
           join organizationUnit in dbContext.Set<OrganizationUnit>()
               on schoolEnrollment.OrganizationId equals organizationUnit.Id
           where schoolEnrollment.PersonId == personId
               && schoolEnrollment.StartDate <= today
               && (schoolEnrollment.EndDate == null || schoolEnrollment.EndDate >= today)
           orderby schoolEnrollment.SchoolingStatusCode == "ACTIVE" descending,
               schoolEnrollment.StartDate descending
           select new EnrollmentProjection(
               schoolEnrollment.Id,
               schoolEnrollment.OrganizationId,
               organizationUnit.UnitCode,
               organizationUnit.UnitName,
               schoolEnrollment.AcademicYear,
               schoolEnrollment.LevelCode,
               schoolEnrollment.ClassCode,
               schoolEnrollment.UpdatedAtUtc);

    private static AdminAccountDetailsProfile ToProfile(
        Person person,
        EnrollmentProjection? enrollment,
        string? identityNumberMasked)
        => new(
            person.Id,
            identityNumberMasked,
            person.OfficialFullName,
            person.DateOfBirth,
            person.NationalityCode,
            person.OfficialAddress,
            person.PreferredAddress,
            person.PreferredEmail,
            person.PreferredMobile,
            enrollment?.OrganizationId,
            enrollment?.OrganizationCode,
            enrollment?.OrganizationName,
            enrollment?.AcademicYear,
            enrollment?.LevelCode,
            enrollment?.ClassCode,
            CalculateUpdatedAtToken(person.UpdatedAtUtc, enrollment?.UpdatedAtUtc));

    private static DateTime CalculateUpdatedAtToken(DateTime personUpdatedAtUtc, DateTime? enrollmentUpdatedAtUtc)
    {
        if (enrollmentUpdatedAtUtc is null || personUpdatedAtUtc >= enrollmentUpdatedAtUtc.Value)
        {
            return personUpdatedAtUtc;
        }

        return enrollmentUpdatedAtUtc.Value;
    }

    private static DateTime TruncateToMilliseconds(DateTime value)
        => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

    private static string? NormalizeNullable(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record EnrollmentProjection(
        long Id,
        long OrganizationId,
        string OrganizationCode,
        string OrganizationName,
        string AcademicYear,
        string LevelCode,
        string? ClassCode,
        DateTime UpdatedAtUtc);
}
