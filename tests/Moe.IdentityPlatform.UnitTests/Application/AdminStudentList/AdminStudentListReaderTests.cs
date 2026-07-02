using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.Infrastructure.Students;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Application.AdminStudentList;

public sealed class AdminStudentListReaderTests
{
    [Fact]
    public async Task ListAsync_ForHqAdmin_ReturnsAllStudentsAcrossSchools()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1001, "Alice Citizen", "S1234001A", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1002, "Ben Citizen", "S1234002B", "CITIZEN", 20, "SEC_2", "2A");
        var accounts = new FakeEducationAccountBulkLookupGateway();
        accounts.Add(1001, "ACTIVE", 10m);
        accounts.Add(1002, "ACTIVE", 20m);
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext, accounts);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [],
            hasGlobalAccess: true,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1001L, 1002L]);
        page.Items.Should().OnlyContain(x => x.NationalityCode == "SG");
    }

    [Fact]
    public async Task ListAsync_ForSchoolAdmin_FiltersAtScopedOrganizations()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1003, "Own School", "S1234003C", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1004, "Other School", "S1234004D", "CITIZEN", 20, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1003);
    }

    [Fact]
    public async Task ListAsync_ForSchoolAdminScopedToMultipleSchools_ReturnsAllScopedSchools()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1005, "School Ten", "S1234005E", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1006, "School Twenty", "S1234006F", "CITIZEN", 20, "SEC_1", "1A");
        SeedStudent(dbContext, 1007, "School Thirty", "S1234007G", "CITIZEN", 30, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [10, 20],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1005L, 1006L]);
    }

    [Fact]
    public async Task ListAsync_SearchWithOneCharacter_IgnoresSearchTerm()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1008, "Alice Search", "S1234008H", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1009, "Bob Search", "S1234009J", "CITIZEN", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(search: "A", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListAsync_SearchWithTwoCharacters_MatchesName()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1010, "Clara Match", "S1234010K", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1011, "Dylan Other", "S1234011L", "CITIZEN", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(search: "Cla", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1010);
    }

    [Fact]
    public async Task ListAsync_SearchWithTwoCharacters_MatchesLastFourNric()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1012, "Nric Match", "S1234567M", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1013, "Nric Other", "S7654321N", "CITIZEN", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(search: "567", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().MaskedNric.Should().Be("S****567M");
    }

    [Fact]
    public async Task ListAsync_FilterByMultipleLevelsAndClass_Composes()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1014, "Level Class Match", "S1234014P", "CITIZEN", 10, "SEC_2", "2A");
        SeedStudent(dbContext, 1015, "Level Only", "S1234015Q", "CITIZEN", 10, "SEC_2", "2B");
        SeedStudent(dbContext, 1031, "Second Level Match", "S1234031H", "CITIZEN", 10, "SEC_3", "2A");
        SeedStudent(dbContext, 1032, "Wrong Level", "S1234032J", "CITIZEN", 10, "SEC_4", "2A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(levelCodes: ["SEC_2", "SEC_3"], classCode: "2A", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1014L, 1031L]);
    }

    [Fact]
    public async Task ListAsync_FilterByCommaSeparatedLevelsAndClass_Composes()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1033, "Comma Level Match", "S1234033K", "CITIZEN", 10, "SEC_2", "2A");
        SeedStudent(dbContext, 1034, "Comma Second Match", "S1234034L", "CITIZEN", 10, "SEC_3", "2A");
        SeedStudent(dbContext, 1035, "Comma Wrong Class", "S1234035M", "CITIZEN", 10, "SEC_2", "2B");
        SeedStudent(dbContext, 1036, "Comma Wrong Level", "S1234036N", "CITIZEN", 10, "SEC_4", "2A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(levelCodes: ["SEC_2, SEC_3"], classCode: "2A", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1033L, 1034L]);
    }

    [Fact]
    public async Task ListAsync_FilterByHigherEducationLevels_Composes()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1037, "Bachelor Match", "S1234037P", "CITIZEN", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1038, "Master Match", "S1234038Q", "CITIZEN", 10, "MASTER", "PG");
        SeedStudent(dbContext, 1039, "Phd Other", "S1234039R", "CITIZEN", 10, "PHD", "DR");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(levelCodes: ["BACHELOR,MASTER"], page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1037L, 1038L]);
    }

    [Fact]
    public async Task ListAsync_FilterByCitizenshipStatus_ReturnsMatchingStudentsOnly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1044, "Citizen Match", "S1234044W", "CITIZEN", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1045, "Permanent Resident Other", "S1234045X", "PR", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1046, "Pass Holder Other", "F1234046Y", "VALID_PASS_HOLDER", 10, "BACHELOR", "UG");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(citizenshipStatusCode: "CITIZEN", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1044);
    }

    [Fact]
    public async Task ListAsync_FilterByInternationalStudent_ReturnsValidPassHoldersOnly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1050, "Citizen Other", "S1234050C", "CITIZEN", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1051, "Pr Other", "S1234051D", "PR", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1052, "Pass Holder Match", "S1234052E", "VALID_PASS_HOLDER", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1053, "Second Pass Holder", "F1234053F", "VALID_PASS_HOLDER", 10, "BACHELOR", "UG");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(citizenshipStatusCode: "VALID_PASS_HOLDER", page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1052L, 1053L]);
    }

    [Fact]
    public async Task ListAsync_CitizenshipStatusAll_DoesNotFilterStudents()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1047, "All Citizen", "S1234047Z", "CITIZEN", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1048, "All Pr", "S1234048A", "PR", 10, "BACHELOR", "UG");
        SeedStudent(dbContext, 1049, "All Pass Holder", "F1234049B", "VALID_PASS_HOLDER", 10, "BACHELOR", "UG");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(citizenshipStatusCode: null, page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Select(x => x.PersonId).Should().BeEquivalentTo([1047L, 1048L, 1049L]);
    }

    [Fact]
    public async Task ListAsync_FilterByNoAccount_ReturnsStudentsWithoutAccountOnly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1016, "No Account", "S1234016R", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1017, "Has Account", "S1234017T", "CITIZEN", 10, "SEC_1", "1A");
        var accounts = new FakeEducationAccountBulkLookupGateway();
        accounts.Add(1017, "ACTIVE", 33m);
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext, accounts);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(accountStatus: AdminStudentAccountStatusFilter.NoAccount, page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1016);
        page.Items.Single().AccountStatusCode.Should().Be("NO_ACCOUNT");
    }

    [Fact]
    public async Task ListAsync_FilterByActiveAccount_ReturnsActiveAccountsOnly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1018, "Active Account", "S1234018U", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1019, "Closed Account", "S1234019V", "CITIZEN", 10, "SEC_1", "1A");
        var accounts = new FakeEducationAccountBulkLookupGateway();
        accounts.Add(1018, "ACTIVE", 33m);
        accounts.Add(1019, "CLOSED", 0m);
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext, accounts);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(accountStatus: AdminStudentAccountStatusFilter.Active, page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1018);
    }

    [Fact]
    public async Task ListAsync_IncludesStudentPortalAccessStatus()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1041, "Disabled Portal", "S1234041T", "CITIZEN", 10, "SEC_1", "1A");
        SetPersonStatus(dbContext, 1041, "DISABLED");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonStatusCode.Should().Be("DISABLED");
    }

    [Fact]
    public async Task ListAsync_FilterByDisabledPortalAccess_ReturnsDisabledUserAccountsOnly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1042, "Disabled Portal", "S1234042U", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1043, "Active Portal", "S1234043V", "CITIZEN", 10, "SEC_1", "1A");
        SetPersonStatus(dbContext, 1042, "DISABLED");
        SetPersonStatus(dbContext, 1043, "ACTIVE");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(portalAccessStatus: AdminStudentPortalAccessStatusFilter.Disabled, page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1042);
    }

    [Fact]
    public async Task ListAsync_ReturnsSchoolNameFromEnrollmentOrganization()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1020, "School Named", "S1234020W", "PR", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().SchoolName.Should().Be("School 10");
    }

    [Fact]
    public async Task ListAsync_FilterByNotEnrolled_ForSchoolAdminRequiresScopedEnrollmentHistory()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1021, "Scoped Former", "S1234021X", "CITIZEN", 10, "SEC_1", "1A", status: "GRADUATED", endDate: Today.AddDays(-10));
        SeedPersonOnly(dbContext, 1022, "Unrelated Account Holder", "S1234022Y", "CITIZEN");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(enrollmentStatus: AdminStudentEnrollmentStatusFilter.NotEnrolled, page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.Items.Should().ContainSingle();
        page.Items.Single().PersonId.Should().Be(1021);
        page.Items.Single().EnrollmentStatusCode.Should().Be("NOT_ENROLLED");
    }

    [Fact]
    public async Task ListAsync_PaginatesWithAccurateTotalCount()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1023, "Alpha Page", "S1234023Z", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1024, "Beta Page", "S1234024A", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1025, "Gamma Page", "S1234025B", "CITIZEN", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 2, pageSize: 1),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Should().ContainSingle();
        page.Items.Single().FullName.Should().Be("Beta Page");
    }

    [Fact]
    public async Task ListAsync_ResponseDoesNotExposeUnmaskedNric()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1026, "Masked Student", "S9876543C", "CITIZEN", 10, "SEC_1", "1A");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        AdminStudentListPage page = await reader.ListAsync(
            AdminStudentListCriteria.Default(page: 1, pageSize: 20),
            scopedOrganizationIds: [10],
            hasGlobalAccess: false,
            Today,
            CancellationToken.None);

        string serialized = System.Text.Json.JsonSerializer.Serialize(page);
        serialized.Should().Contain("S****543C");
        serialized.Should().NotContain("S9876543C");
    }

    [Fact]
    public async Task ListClassesAsync_ReturnsDistinctActiveClassCodesForScopedLevel()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1027, "Class A", "S1234027D", "CITIZEN", 10, "SEC_1", "1A");
        SeedStudent(dbContext, 1028, "Class B", "S1234028E", "CITIZEN", 10, "SEC_1", "1B");
        SeedStudent(dbContext, 1029, "Class Other Level", "S1234029F", "CITIZEN", 10, "SEC_2", "2A");
        SeedStudent(dbContext, 1030, "Class Other Scope", "S1234030G", "CITIZEN", 20, "SEC_1", "1Z");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        IReadOnlyList<string> classes = await reader.ListClassesAsync(
            organizationId: 10,
            levelCode: "SEC_1",
            Today,
            CancellationToken.None);

        classes.Should().BeEquivalentTo("1A", "1B");
    }

    [Fact]
    public async Task ListClassesAsync_ForHigherEducationLevelWithoutClassSubdivision_ReturnsEmpty()
    {
        using MoeDbContext dbContext = CreateDbContext();
        SeedStudent(dbContext, 1040, "Bachelor No Class", "S1234040S", "CITIZEN", 10, "BACHELOR", "");
        await dbContext.SaveChangesAsync();
        AdminStudentListReader reader = CreateReader(dbContext);

        IReadOnlyList<string> classes = await reader.ListClassesAsync(
            organizationId: 10,
            levelCode: "BACHELOR",
            Today,
            CancellationToken.None);

        classes.Should().BeEmpty();
    }

    private static readonly DateOnly Today = new(2026, 6, 22);

    private static AdminStudentListReader CreateReader(
        MoeDbContext dbContext,
        IEducationAccountBulkLookupGateway? accounts = null)
        => new(dbContext, accounts ?? new FakeEducationAccountBulkLookupGateway());

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IModelConfigurationContributor[] contributors =
        [
            new IdentityPlatformModelConfiguration(),
        ];

        return new MoeDbContext(options, contributors);
    }

    private static void SeedStudent(
        MoeDbContext dbContext,
        long personId,
        string fullName,
        string nricMasked,
        string residencyCode,
        long organizationId,
        string levelCode,
        string classCode,
        string status = "ACTIVE",
        DateOnly? endDate = null)
    {
        EnsureSchool(dbContext, organizationId);
        SeedPersonOnly(dbContext, personId, fullName, nricMasked, residencyCode);

        SchoolEnrollment enrollment = new(
            personId,
            organizationId,
            $"STU-{personId}",
            "2026",
            levelCode,
            classCode,
            new DateOnly(2026, 1, 1),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        SetId(enrollment, personId + 10000);
        SetProperty(enrollment, nameof(SchoolEnrollment.SchoolingStatusCode), status);
        SetProperty(enrollment, nameof(SchoolEnrollment.EndDate), endDate);
        dbContext.Set<SchoolEnrollment>().Add(enrollment);
    }

    private static void EnsureSchool(MoeDbContext dbContext, long organizationId)
    {
        if (dbContext.Set<OrganizationUnit>().Local.Any(x => x.Id == organizationId))
        {
            return;
        }

        OrganizationUnit school = new(
            $"SCH-{organizationId}",
            $"School {organizationId}",
            "SCHOOL",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetId(school, organizationId);
        dbContext.Set<OrganizationUnit>().Add(school);
    }

    private static void SeedPersonOnly(
        MoeDbContext dbContext,
        long personId,
        string fullName,
        string nricMasked,
        string residencyCode)
    {
        Person person = new(personId, $"P-{personId}", fullName, new DateOnly(2010, 1, 1), "SG", residencyCode);
        SetProperty(person, nameof(Person.IdentityNumberMasked), nricMasked);
        dbContext.Set<Person>().Add(person);
    }

    private static void SetPersonStatus(MoeDbContext dbContext, long personId, string statusCode)
    {
        Person person = dbContext.Set<Person>().Local.Single(x => x.Id == personId);
        SetProperty(person, nameof(Person.PersonStatusCode), statusCode);
    }

    private static void SetId(object entity, long id)
        => entity.GetType().GetProperty("Id")!.SetValue(entity, id);

    private static void SetProperty(object entity, string propertyName, object? value)
        => entity.GetType()
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, value);

    private sealed class FakeEducationAccountBulkLookupGateway : IEducationAccountBulkLookupGateway
    {
        private readonly Dictionary<long, EducationAccountLookupSummary> _accounts = [];

        public void Add(long personId, string statusCode, decimal balance)
            => _accounts[personId] = new EducationAccountLookupSummary(
                personId + 50000,
                personId,
                $"EA-{personId}",
                "SGD",
                statusCode,
                balance);

        public Task<IReadOnlyDictionary<long, EducationAccountLookupSummary>> FindByPersonIdsAsync(
            IReadOnlyCollection<long> personIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyDictionary<long, EducationAccountLookupSummary>>(
                _accounts
                    .Where(x => personIds.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value));
    }
}
