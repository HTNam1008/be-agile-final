using System.Collections;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
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
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == personId);
        object identifier = SingleEntity(
            db,
            "Moe.Modules.IdentityPlatform.Domain.People.PersonIdentifier",
            x => (long)GetProperty(x, "PersonId")! == personId);

        Assert.Equal("Manual Student", person.OfficialFullName);
        Assert.Equal(1, enrollment.OrganizationId);
        Assert.Equal(request.StudentNumber, enrollment.StudentNumber);
        Assert.Equal("IDENTITY_NUMBER", GetProperty(identifier, "IdentifierTypeCode"));
        Assert.Equal(request.IdentityNumber, GetProperty(identifier, "IdentifierMasked"));
        Assert.Equal($"PSEA-{personId:D8}", account.AccountNumber);
        Assert.Equal(0m, account.CachedBalance);
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
    public async Task HqAdmin_Should_Be_Asked_For_SchoolName()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = CreateRequest(
            schoolName: null,
            identityNumber: $"F{suffix[..7]}C",
            studentNumber: $"IT-SYS-MISSING-{suffix}");

        using HttpRequestMessage message = new(HttpMethod.Post, "/api/admin/v1/students");
        message.Headers.Add("X-Test-Role", "HQ_ADMIN");
        message.Content = JsonContent.Create(request);

        using HttpResponseMessage response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("IDENTITY.SCHOOL_NAME_REQUIRED", body);
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
        Assert.Contains($"PSEA-{personId:D8}", accountBody);
        Assert.Contains("\"currentBalance\":0", accountBody);

        using HttpResponseMessage dashboardResponse = await SendEServiceGetAsync(
            "/api/eservice/v1/dashboard",
            login);

        await AssertStatusAsync(HttpStatusCode.OK, dashboardResponse);
        string dashboardBody = await dashboardResponse.Content.ReadAsStringAsync();
        Assert.Contains("Manual Student", dashboardBody);
        Assert.Contains("Demo Secondary School", dashboardBody);
        Assert.Contains($"PSEA-{personId:D8}", dashboardBody);
        Assert.Contains("\"currentBalance\":0", dashboardBody);
    }

    private static CreateStudentRequestBody CreateRequest(
        string? schoolName,
        string identityNumber,
        string studentNumber)
    {
        return new CreateStudentRequestBody(
            schoolName,
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
        string IdentityNumber,
        string FullName,
        DateOnly DateOfBirth,
        string NationalityCode,
        string CitizenshipStatusCode,
        string StudentNumber,
        string AcademicYear,
        string LevelCode,
        string ClassCode,
        DateOnly? StartDate,
        string? Email,
        string? Mobile,
        string? Address,
        bool IsAccountHolder);
}
