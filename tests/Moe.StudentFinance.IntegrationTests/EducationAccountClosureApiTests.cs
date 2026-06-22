using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountClosureApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SchoolAdmin_Can_Close_Own_School_Education_Account()
    {
        long accountId = await SeedAccountAsync(organizationId: 1);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/education-accounts/{accountId}/close",
            CloseBody());

        await AssertStatusAsync(HttpStatusCode.OK, response);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.Id == accountId);
        Assert.Equal(AccountStatuses.Closed, account.StatusCode);
        Assert.Equal(EducationAccountClosingReasonCodes.StudentIneligible, account.ClosingReasonCode);
        Assert.Equal(1001, account.ClosedByLoginAccountId);
    }

    [Fact]
    public async Task SchoolAdmin_Cannot_Close_OutOfScope_Education_Account()
    {
        long accountId = await SeedAccountAsync(organizationId: 2);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/education-accounts/{accountId}/close",
            CloseBody());

        await AssertStatusAsync(HttpStatusCode.Forbidden, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AUTH.ORGANIZATION_OUTSIDE_SCOPE", body);
    }

    [Fact]
    public async Task Close_Unknown_Education_Account_Returns_NotFound()
    {
        long missingId = Random.Shared.NextInt64(9_000_000, 9_999_999);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/education-accounts/{missingId}/close",
            CloseBody());

        await AssertStatusAsync(HttpStatusCode.NotFound, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ACCOUNT.NOT_FOUND", body);
    }

    [Fact]
    public async Task Close_AlreadyClosed_Education_Account_Returns_Conflict()
    {
        long accountId = await SeedAccountAsync(organizationId: 1, closed: true);

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/education-accounts/{accountId}/close",
            CloseBody());

        await AssertStatusAsync(HttpStatusCode.Conflict, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ACCOUNT.ALREADY_CLOSED", body);
    }

    private async Task<long> SeedAccountAsync(long organizationId, bool closed = false)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        long personId = Random.Shared.NextInt64(600_000, 900_000);

        db.Set<Person>().Add(new Person(
            personId,
            $"UM004-{personId}",
            $"UM004 Student {personId}",
            new DateOnly(2010, 1, 1),
            "SG",
            "CITIZEN"));

        SchoolEnrollment enrollment = (SchoolEnrollment)Activator.CreateInstance(typeof(SchoolEnrollment), nonPublic: true)!;
        Set(enrollment, nameof(SchoolEnrollment.PersonId), personId);
        Set(enrollment, nameof(SchoolEnrollment.OrganizationId), organizationId);
        Set(enrollment, nameof(SchoolEnrollment.StudentNumber), $"UM004-{personId}");
        Set(enrollment, nameof(SchoolEnrollment.AcademicYear), "2026");
        Set(enrollment, nameof(SchoolEnrollment.LevelCode), "SEC_1");
        Set(enrollment, nameof(SchoolEnrollment.ClassCode), "1A");
        Set(enrollment, nameof(SchoolEnrollment.SchoolingStatusCode), "ACTIVE");
        Set(enrollment, nameof(SchoolEnrollment.StartDate), new DateOnly(2026, 1, 1));
        Set(enrollment, nameof(SchoolEnrollment.EndDate), null);
        Set(enrollment, nameof(SchoolEnrollment.SourceCode), "UM004_TEST");
        Set(enrollment, nameof(SchoolEnrollment.CreatedAtUtc), DateTime.UtcNow);
        Set(enrollment, nameof(SchoolEnrollment.UpdatedAtUtc), DateTime.UtcNow);
        db.Set<SchoolEnrollment>().Add(enrollment);

        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-UM004-{personId}",
            DateTimeOffset.UtcNow,
            "EXCEPTION",
            "Integration test",
            1001).Value;

        if (closed)
        {
            Assert.True(account.CloseManual(
                DateTimeOffset.UtcNow,
                EducationAccountClosingReasonCodes.AdminError,
                "Pre-closed for integration test",
                1001).IsSuccess);
        }

        db.Set<EducationAccount>().Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static object CloseBody()
        => new
        {
            reasonCode = EducationAccountClosingReasonCodes.StudentIneligible,
            remarks = "Student no longer eligible"
        };

    private static void Set(SchoolEnrollment enrollment, string propertyName, object? value)
        => typeof(SchoolEnrollment)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(enrollment, value);

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        Assert.Fail($"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
    }
}
