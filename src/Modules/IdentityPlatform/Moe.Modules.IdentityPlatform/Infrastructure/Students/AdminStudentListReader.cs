using Microsoft.EntityFrameworkCore;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Students;

internal sealed class AdminStudentListReader(
    MoeDbContext dbContext,
    IEducationAccountBulkLookupGateway accounts) : IAdminStudentListReader
{
    private const string NotEnrolled = "NOT_ENROLLED";
    private const string NoAccount = "NO_ACCOUNT";

    public async Task<AdminStudentListPage> ListAsync(
        AdminStudentListCriteria criteria,
        IReadOnlyCollection<long> scopedOrganizationIds,
        bool hasGlobalAccess,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(criteria.Page, 1);
        int pageSize = Math.Clamp(criteria.PageSize, 1, 100);

        List<SchoolEnrollment> enrollments = await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => hasGlobalAccess || scopedOrganizationIds.Contains(x.OrganizationId))
            .ToListAsync(cancellationToken);

        IQueryable<Person> peopleQuery = dbContext.Set<Person>().AsNoTracking();
        if (!hasGlobalAccess)
        {
            peopleQuery = peopleQuery.Where(person => dbContext.Set<SchoolEnrollment>()
                .AsNoTracking()
                .Any(enrollment => enrollment.PersonId == person.Id
                    && scopedOrganizationIds.Contains(enrollment.OrganizationId)));
        }

        List<Person> people = await peopleQuery.ToListAsync(cancellationToken);

        var enrollmentByPersonId = enrollments
            .GroupBy(x => x.PersonId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderByDescending(e => IsActiveCurrent(e, today))
                    .ThenByDescending(e => e.StartDate)
                    .ThenByDescending(e => e.Id)
                    .First());

        IReadOnlyDictionary<long, EducationAccountLookupSummary> accountByPersonId =
            await accounts.FindByPersonIdsAsync(people.Select(x => x.Id).ToArray(), cancellationToken);

        IEnumerable<Row> rows = people.Select(person =>
        {
            enrollmentByPersonId.TryGetValue(person.Id, out SchoolEnrollment? enrollment);
            accountByPersonId.TryGetValue(person.Id, out EducationAccountLookupSummary? account);
            bool enrolled = enrollment is not null && IsActiveCurrent(enrollment, today);
            return new Row(person, enrollment, account, enrolled);
        });

        rows = ApplyFilters(rows, criteria);

        long total = rows.LongCount();
        AdminStudentListItem[] items = rows
            .OrderBy(x => x.Person.OfficialFullName)
            .ThenBy(x => x.Person.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToItem)
            .ToArray();

        return new AdminStudentListPage(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<string>> ListClassesAsync(
        long organizationId,
        string levelCode,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        string normalizedLevel = levelCode.Trim().ToUpperInvariant();
        return await dbContext.Set<SchoolEnrollment>()
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId
                && x.LevelCode == normalizedLevel
                && x.SchoolingStatusCode == "ACTIVE"
                && x.StartDate <= today
                && (x.EndDate == null || x.EndDate >= today))
            .Select(x => x.ClassCode)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);
    }

    private static IEnumerable<Row> ApplyFilters(IEnumerable<Row> rows, AdminStudentListCriteria criteria)
    {
        string? search = criteria.Search?.Trim();
        if (search is { Length: >= 2 })
        {
            rows = rows.Where(x => x.Person.OfficialFullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || LastFourSearchValue(x.Person.IdentityNumberMasked).Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(criteria.LevelCode))
        {
            string levelCode = criteria.LevelCode.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.Enrollment?.LevelCode == levelCode);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClassCode))
        {
            string classCode = criteria.ClassCode.Trim().ToUpperInvariant();
            rows = rows.Where(x => x.Enrollment?.ClassCode == classCode);
        }

        rows = criteria.Residency switch
        {
            AdminStudentResidencyFilter.SingaporeCitizen => rows.Where(x => x.Person.CitizenshipStatusCode == ResidencyStatusCodes.Citizen),
            AdminStudentResidencyFilter.PermanentResident => rows.Where(x => x.Person.CitizenshipStatusCode == ResidencyStatusCodes.PermanentResident),
            AdminStudentResidencyFilter.Foreigner => rows.Where(x => x.Person.CitizenshipStatusCode == ResidencyStatusCodes.ValidPassHolder),
            _ => rows
        };

        rows = criteria.EnrollmentStatus switch
        {
            AdminStudentEnrollmentStatusFilter.Enrolled => rows.Where(x => x.IsEnrolled),
            AdminStudentEnrollmentStatusFilter.NotEnrolled => rows.Where(x => !x.IsEnrolled),
            _ => rows
        };

        rows = criteria.AccountStatus switch
        {
            AdminStudentAccountStatusFilter.Active => rows.Where(x => x.Account?.AccountStatusCode == "ACTIVE"),
            AdminStudentAccountStatusFilter.PendingClosure => rows.Where(x => x.Account?.AccountStatusCode == "CLOSING"),
            AdminStudentAccountStatusFilter.Closed => rows.Where(x => x.Account?.AccountStatusCode == "CLOSED"),
            AdminStudentAccountStatusFilter.NoAccount => rows.Where(x => x.Account is null),
            _ => rows
        };

        return rows;
    }

    private static AdminStudentListItem ToItem(Row row)
        => new(
            row.Person.Id,
            MaskNric(row.Person.IdentityNumberMasked),
            row.Person.OfficialFullName,
            row.Enrollment?.LevelCode,
            row.Enrollment?.ClassCode,
            row.Account?.AccountStatusCode ?? NoAccount,
            row.Account?.CurrentBalance,
            row.Person.CitizenshipStatusCode,
            row.IsEnrolled ? row.Enrollment!.SchoolingStatusCode : NotEnrolled,
            row.Enrollment?.OrganizationId);

    private static bool IsActiveCurrent(SchoolEnrollment enrollment, DateOnly today)
        => enrollment.SchoolingStatusCode == "ACTIVE"
            && enrollment.StartDate <= today
            && (enrollment.EndDate == null || enrollment.EndDate >= today);

    private static string LastFourSearchValue(string? maskedNric)
    {
        if (string.IsNullOrWhiteSpace(maskedNric))
        {
            return string.Empty;
        }

        string trimmed = maskedNric.Trim();
        return trimmed.Length <= 4 ? trimmed : trimmed[^4..];
    }

    private static string? MaskNric(string? nric)
    {
        if (string.IsNullOrWhiteSpace(nric))
        {
            return null;
        }

        string value = nric.Trim().ToUpperInvariant();
        if (value.Length <= 5)
        {
            return value;
        }

        return $"{value[0]}****{value[^4..]}";
    }

    private sealed record Row(
        Person Person,
        SchoolEnrollment? Enrollment,
        EducationAccountLookupSummary? Account,
        bool IsEnrolled);
}
