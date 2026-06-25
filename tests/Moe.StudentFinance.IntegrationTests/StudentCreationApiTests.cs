using System.Collections;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Audit;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class StudentCreationApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SchoolAdmin_Should_Create_Student_In_Own_School_Without_SchoolName()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"S{suffix[..7]}A",
            studentNumber: $"IT-MANUAL-{suffix}");

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        Person person = await db.Set<Person>().SingleAsync(x => x.Id == personId);
        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        object identifier = SingleEntity(
            db,
            "Moe.Modules.IdentityPlatform.Domain.People.PersonIdentifier",
            x => (long)GetProperty(x, "PersonId")! == personId);

        Assert.Equal("Manual Student", person.OfficialFullName);
        Assert.Equal(1, enrollment.OrganizationId);
        Assert.Equal(request.StudentNumber, enrollment.StudentNumber);
        Assert.Equal("IDENTITY_NUMBER", GetProperty(identifier, "IdentifierTypeCode"));
        Assert.Equal(request.IdentityNumber, GetProperty(identifier, "IdentifierMasked"));
        Assert.False(await db.Set<EducationAccount>().AnyAsync(x => x.PersonId == personId));
    }

    [Fact]
    public async Task CreateStudent_Should_Leave_Student_Without_EducationAccount_Even_When_IsAccountHolder_Is_False()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"N{suffix[..7]}A",
            studentNumber: $"IT-NO-AH-{suffix}",
            isAccountHolder: false);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        Assert.False(await db.Set<EducationAccount>().AnyAsync(x => x.PersonId == personId));
        Assert.False(AnyEntity(
            db,
            "Moe.Modules.IdentityPlatform.Domain.Audit.AuditLog",
            x => (string)GetProperty(x, "ActionCode")! == AuditActionCodes.EducationAccountCreatedManually
                && (string)GetProperty(x, "ChangedFieldsJson")! != null
                && ((string)GetProperty(x, "ChangedFieldsJson")!).Contains($"\"personId\":{personId}")));
    }

    [Fact]
    public async Task TwoStepCreation_Should_Create_Student_Then_Open_EducationAccount()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"Z{suffix[..7]}A",
            studentNumber: $"IT-TWO-STEP-{suffix}");

        using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        long personId = await ReadPersonIdAsync(createResponse);

        using HttpResponseMessage openResponse = await OpenEducationAccountAsync(personId);

        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal("ACTIVE", account.StatusCode);

        object auditLog = SingleEntity(
            db,
            "Moe.Modules.IdentityPlatform.Domain.Audit.AuditLog",
            x => (string)GetProperty(x, "ActionCode")! == AuditActionCodes.EducationAccountCreatedManually
                && (string)GetProperty(x, "EntityTypeCode")! == "EducationAccount"
                && (long?)GetProperty(x, "EntityId") == account.Id);

        Assert.Contains($"\"personId\":{personId}", (string)GetProperty(auditLog, "ChangedFieldsJson")!);
    }

    [Fact]
    public async Task CreatedStudent_Should_Appear_In_NoAccount_Filter_When_Second_Step_Is_Not_Called()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string studentNumber = $"IT-NO-ACCOUNT-{suffix}";
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"K{suffix[..7]}A",
            studentNumber: studentNumber);

        using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        long personId = await ReadPersonIdAsync(createResponse);

        string identitySearch = request.IdentityNumber[^4..];

        using HttpResponseMessage listResponse = await _client.GetAsync(
            $"/api/admin/v1/students?accountStatus=NoAccount&search={identitySearch}&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        string body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains($"\"personId\":{personId}", body);
        Assert.Contains("NO_ACCOUNT", body);
    }

    [Fact]
    public async Task HqAdmin_Should_Create_Student_When_SchoolName_Is_Provided()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: "Demo Secondary School",
            identityNumber: $"T{suffix[..7]}B",
            studentNumber: $"IT-SYS-{suffix}");

        using HttpRequestMessage message = new(HttpMethod.Post, "/api/admin/v1/students");
        message.Headers.Add("X-Test-Role", "HQ_ADMIN");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal(1, enrollment.OrganizationId);
        Assert.Equal(request.StudentNumber, enrollment.StudentNumber);
    }

    [Fact]
    public async Task CreateStudent_Should_Create_When_ClassCode_Is_Not_Provided()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"C{suffix[..7]}B",
            studentNumber: $"IT-NO-CLASS-{suffix}") with
        {
            ClassCode = null
        };

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal("SEC_4", enrollment.LevelCode);
        Assert.Null(enrollment.ClassCode);
    }

    [Fact]
    public async Task HqAdmin_Should_Create_Student_When_OrganizationId_Is_Provided()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            organizationId: 2,
            identityNumber: $"O{suffix[..7]}B",
            studentNumber: $"IT-ORG-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, role: "HQ_ADMIN");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal(2, enrollment.OrganizationId);
        Assert.Equal(request.StudentNumber, enrollment.StudentNumber);
    }

    [Fact]
    public async Task SchoolAdmin_Should_Create_Student_When_OrganizationId_Is_In_Scope()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            organizationId: 1,
            identityNumber: $"P{suffix[..7]}B",
            studentNumber: $"IT-IN-SCOPE-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, organizationUnitIds: "1,2");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long personId = await ReadPersonIdAsync(response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SchoolEnrollment enrollment = await db.Set<SchoolEnrollment>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal(1, enrollment.OrganizationId);
    }

    [Fact]
    public async Task SchoolAdmin_Should_Be_Denied_When_OrganizationId_Is_Out_Of_Scope()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            organizationId: 2,
            identityNumber: $"Q{suffix[..7]}B",
            studentNumber: $"IT-OUT-SCOPE-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, organizationUnitIds: "1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_OUTSIDE_SCOPE", body);
    }

    [Fact]
    public async Task CreateStudent_Should_Return_NotFound_When_OrganizationId_Does_Not_Exist()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            organizationId: 999999,
            identityNumber: $"R{suffix[..7]}B",
            studentNumber: $"IT-MISSING-ORG-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, role: "HQ_ADMIN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.ORGANIZATION_UNIT_NOT_FOUND", body);
    }

    [Fact]
    public async Task CreateStudent_Should_Create_When_OrganizationId_And_SchoolName_Match()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: "Demo Secondary School",
            organizationId: 1,
            identityNumber: $"U{suffix[..7]}B",
            studentNumber: $"IT-MATCH-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, role: "HQ_ADMIN");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_Should_Reject_When_OrganizationId_And_SchoolName_Conflict()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: "Other Secondary School",
            organizationId: 1,
            identityNumber: $"V{suffix[..7]}B",
            studentNumber: $"IT-CONFLICT-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, role: "HQ_ADMIN");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_IDENTIFIERS_CONFLICT", body);
    }

    [Fact]
    public async Task SchoolName_Legacy_Path_Should_Still_Check_SchoolAdmin_Scope()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: "Other Secondary School",
            identityNumber: $"W{suffix[..7]}B",
            studentNumber: $"IT-NAME-SCOPE-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, organizationUnitIds: "1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_OUTSIDE_SCOPE", body);
    }

    [Fact]
    public async Task HqAdmin_Should_Be_Asked_For_School()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"F{suffix[..7]}C",
            studentNumber: $"IT-SYS-MISSING-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, role: "HQ_ADMIN");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_REQUIRED", body);
    }

    [Fact]
    public async Task SchoolAdmin_With_No_Scope_Should_Fail_Closed_When_School_Is_Not_Provided()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"X{suffix[..7]}C",
            studentNumber: $"IT-NO-SCOPE-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, organizationUnitIds: "none");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_NAME_REQUIRED", body);
    }

    [Fact]
    public async Task SchoolAdmin_With_Multiple_Scopes_Should_Be_Asked_For_School_When_School_Is_Not_Provided()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"Y{suffix[..7]}C",
            studentNumber: $"IT-MULTI-SCOPE-{suffix}");

        using HttpResponseMessage response = await SendCreateStudentAsync(request, organizationUnitIds: "1,2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_SCOPE_AMBIGUOUS", body);
    }

    [Fact]
    public async Task CreatedStudent_Should_Login_And_View_Profile_Account_And_Dashboard()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"G{suffix[..7]}D",
            studentNumber: $"IT-LOGIN-{suffix}");

        using HttpResponseMessage createResponse = await _client.PostAsJsonAsync(
            "/api/admin/v1/students",
            request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        long personId = await ReadPersonIdAsync(createResponse);

        using HttpResponseMessage openResponse = await OpenEducationAccountAsync(personId);
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);

        EServiceLoginResolution login = await ResolveMockPassLoginAsync(
            request.IdentityNumber,
            request.FullName);

        Assert.Equal(personId, login.PersonId);
        Assert.True(login.UserAccountId > 0);

        using HttpResponseMessage meResponse = await SendEServiceGetAsync(
            "/api/eservice/v1/me",
            login);

        await AssertStatusAsync(HttpStatusCode.OK, meResponse);
        string meBody = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains($"\"personId\":{personId}", meBody);
        Assert.Contains("\"portalAccessCode\":\"ESERVICE\"", meBody);
        Assert.Contains("\"STUDENT\"", meBody);

        using HttpResponseMessage profileResponse = await SendEServiceGetAsync(
            "/api/eservice/v1/profile",
            login);

        await AssertStatusAsync(HttpStatusCode.OK, profileResponse);
        string profileBody = await profileResponse.Content.ReadAsStringAsync();
        Assert.Contains("Manual Student", profileBody);
        Assert.Contains("Demo Secondary School", profileBody);

        using HttpResponseMessage accountResponse = await SendEServiceGetAsync(
            "/api/eservice/v1/my-education-account",
            login);

        await AssertStatusAsync(HttpStatusCode.OK, accountResponse);
        string accountBody = await accountResponse.Content.ReadAsStringAsync();
        Assert.Contains($"\"personId\":{personId}", accountBody);
        Assert.Contains("\"accountNumber\"", accountBody);
        Assert.Contains("\"currentBalance\":0", accountBody);

        using HttpResponseMessage dashboardResponse = await SendEServiceGetAsync(
            "/api/eservice/v1/dashboard",
            login);

        await AssertStatusAsync(HttpStatusCode.OK, dashboardResponse);
        string dashboardBody = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("Manual Student", dashboardBody);
        Assert.Contains("Demo Secondary School", dashboardBody);
        Assert.Contains("\"educationAccount\"", dashboardBody);
        Assert.Contains("\"currentBalance\":0", dashboardBody);
    }

    private static CreateStudentRequestBody CreateRequest(
        string? schoolName,
        string identityNumber,
        string studentNumber)
    {
        return new CreateStudentRequestBody(
            schoolName,
            null,
            identityNumber,
            "Manual Student",
            new DateOnly(2008, 5, 12),
            "SG",
            "CITIZEN",
            studentNumber,
            "2026",
            "SEC_4",
            "4A",
            new DateOnly(2026, 1, 2),
            "manual.student@example.com",
            "+6591234567",
            "Integration test address",
            true);
    }

    private static CreateStudentRequestBody CreateRequest(
        string? schoolName,
        long? organizationId,
        string identityNumber,
        string studentNumber)
    {
        return new CreateStudentRequestBody(
            schoolName,
            organizationId,
            identityNumber,
            "Manual Student",
            new DateOnly(2008, 5, 12),
            "SG",
            "CITIZEN",
            studentNumber,
            "2026",
            "SEC_4",
            "4A",
            new DateOnly(2026, 1, 2),
            "manual.student@example.com",
            "+6591234567",
            "Integration test address",
            true);
    }

    private static CreateStudentRequestBody CreateRequest(
        string? schoolName,
        string identityNumber,
        string studentNumber,
        bool isAccountHolder)
    {
        return new CreateStudentRequestBody(
            schoolName,
            null,
            identityNumber,
            "Manual Student",
            new DateOnly(2008, 5, 12),
            "SG",
            "CITIZEN",
            studentNumber,
            "2026",
            "SEC_4",
            "4A",
            new DateOnly(2026, 1, 2),
            "manual.student@example.com",
            "+6591234567",
            "Integration test address",
            isAccountHolder);
    }

    private async Task<HttpResponseMessage> SendCreateStudentAsync(
        CreateStudentRequestBody request,
        string role = "SCHOOL_ADMIN",
        string? organizationUnitIds = null)
    {
        using HttpRequestMessage message = new(HttpMethod.Post, "/api/admin/v1/students");
        message.Headers.Add("X-Test-Role", role);

        if (organizationUnitIds is not null)
        {
            message.Headers.Add("X-Test-OrganizationUnitIds", organizationUnitIds);
        }

        message.Content = JsonContent.Create(request);
        return await _client.SendAsync(message);
    }

    private async Task<HttpResponseMessage> OpenEducationAccountAsync(long personId)
    {
        return await _client.PostAsJsonAsync(
            "/api/admin/v1/education-accounts",
            new
            {
                personId,
                reasonCode = "EXCEPTION",
                remarks = "Manual integration test open account"
            });
    }

    private static async Task<long> ReadPersonIdAsync(HttpResponseMessage response)
    {
        using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("data").GetProperty("personId").GetInt64();
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }

    private async Task<EServiceLoginResolution> ResolveMockPassLoginAsync(
        string identityNumber,
        string displayName)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IEServiceLoginResolver resolver = scope.ServiceProvider.GetRequiredService<IEServiceLoginResolver>();

        return await resolver.ResolveAsync(
            new SingpassLoginResult(
                "http://localhost:5156/singpass/v3/fapi",
                $"mockpass-sub-{Guid.NewGuid():N}",
                identityNumber,
                displayName,
                "urn:mockpass:loa2",
                "pwd"),
            CancellationToken.None);
    }

    private async Task<HttpResponseMessage> SendEServiceGetAsync(
        string requestUri,
        EServiceLoginResolution login)
    {
        using HttpRequestMessage message = new(HttpMethod.Get, requestUri);
        message.Headers.Add("X-Test-PersonId", login.PersonId.ToString());
        message.Headers.Add("X-Test-UserAccountId", login.UserAccountId.ToString());
        return await _client.SendAsync(message);
    }

    private static object SingleEntity(
        MoeDbContext db,
        string typeName,
        Func<object, bool> predicate)
    {
        Type entityType = typeof(Person).Assembly.GetType(typeName, throwOnError: true)!;
        IQueryable query = CreateQueryable(db, entityType);
        return query.Cast<object>().Single(predicate);
    }

    private static bool AnyEntity(
        MoeDbContext db,
        string typeName,
        Func<object, bool> predicate)
    {
        Type entityType = typeof(Person).Assembly.GetType(typeName, throwOnError: true)!;
        IQueryable query = CreateQueryable(db, entityType);
        return query.Cast<object>().Any(predicate);
    }

    private static IQueryable CreateQueryable(MoeDbContext db, Type entityType)
    {
        MethodInfo setMethod = typeof(DbContext)
            .GetMethods()
            .Single(x => x.Name == nameof(DbContext.Set)
                && x.IsGenericMethod
                && x.GetParameters().Length == 0);

        return (IQueryable)setMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
    }

    private static object? GetProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName)!.GetValue(target);
    }

    private sealed record CreateStudentRequestBody(
        string? SchoolName,
        long? OrganizationId,
        string IdentityNumber,
        string FullName,
        DateOnly DateOfBirth,
        string NationalityCode,
        string CitizenshipStatusCode,
        string StudentNumber,
        string AcademicYear,
        string LevelCode,
        string? ClassCode,
        DateOnly? StartDate,
        string? Email,
        string? Mobile,
        string? Address,
        bool IsAccountHolder);
}
