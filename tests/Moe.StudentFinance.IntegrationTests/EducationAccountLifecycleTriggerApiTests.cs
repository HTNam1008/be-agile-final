using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountLifecycleTriggerApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RunNow_WithUnsupportedRole_ReturnsForbidden()
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "/api/admin/v1/education-account-lifecycle/run-now");
        request.Headers.Add("X-Test-Role", "STUDENT");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RunNow_WithAdminRoleWithoutLifecycleManualTriggerPermission_RunsLifecycleSynchronously()
    {
        (long openingPersonId, long closingAccountId) = await SeedLifecycleCandidatesAsync();
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "/api/admin/v1/education-account-lifecycle/run-now");

        using HttpResponseMessage response = await _client.SendAsync(request);

        await AssertStatusAsync(HttpStatusCode.OK, response);
        ApiResponse<LifecycleRunNowResponse>? envelope =
            await response.Content.ReadFromJsonAsync<ApiResponse<LifecycleRunNowResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.True(envelope.Data.OpenedCount >= 1);
        Assert.True(envelope.Data.ClosedCount >= 1);
        Assert.True(envelope.Data.RunAtUtc > DateTimeOffset.MinValue);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Assert.True(await db.Set<EducationAccount>().AnyAsync(x => x.PersonId == openingPersonId));
        EducationAccount closedAccount = await db.Set<EducationAccount>().SingleAsync(x => x.Id == closingAccountId);
        Assert.Equal(AccountStatuses.Closed, closedAccount.StatusCode);
        Assert.Equal(EducationAccountClosingReasonCodes.AutoAgeLimit, closedAccount.ClosingReasonCode);
    }

    private async Task<(long OpeningPersonId, long ClosingAccountId)> SeedLifecycleCandidatesAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        long openingPersonId = Random.Shared.NextInt64(1_100_000, 1_199_999);
        long closingPersonId = Random.Shared.NextInt64(1_200_000, 1_299_999);

        db.Set<Person>().Add(new Person(
            openingPersonId,
            $"AUTO003-OPEN-{openingPersonId}",
            $"AUTO003 Open Student {openingPersonId}",
            new DateOnly(2008, 1, 1),
            "SG",
            "CITIZEN"));

        db.Set<Person>().Add(new Person(
            closingPersonId,
            $"AUTO003-CLOSE-{closingPersonId}",
            $"AUTO003 Close Student {closingPersonId}",
            new DateOnly(1990, 1, 1),
            "SG",
            "CITIZEN"));

        EducationAccount closingAccount = EducationAccount.OpenManual(
            closingPersonId,
            $"EA-AUTO003-{closingPersonId}",
            DateTimeOffset.UtcNow,
            "EXCEPTION",
            "Integration lifecycle trigger seed",
            1001).Value;

        db.Set<EducationAccount>().Add(closingAccount);
        await db.SaveChangesAsync();
        return (openingPersonId, closingAccount.Id);
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

    private sealed record LifecycleRunNowResponse(
        int OpenedCount,
        int ClosedCount,
        DateTimeOffset RunAtUtc);
}
