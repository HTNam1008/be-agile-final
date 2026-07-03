using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class PublicFasApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Schools_is_anonymous_and_returns_active_schools_for_the_public_form()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/v1/fas/schools");
        request.Headers.Add("X-Test-Anonymous", "true");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement schools = document.RootElement.GetProperty("data");
        Assert.Contains(schools.EnumerateArray(), school =>
            school.GetProperty("organizationId").GetInt64() == 1 &&
            school.GetProperty("organizationName").GetString() == "Demo Secondary School");
    }

    [Fact]
    public async Task Search_is_anonymous_and_returns_the_contract_used_by_the_public_page()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/public/v1/fas/search");
        request.Headers.Add("X-Test-Anonymous", "true");
        request.Content = JsonContent.Create(new
        {
            organizationId = 1,
            monthlyHouseholdIncome = 3500m,
            householdMemberCount = 4
        });

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement result = document.RootElement.GetProperty("data");
        Assert.Equal("Demo Secondary School", result.GetProperty("school").GetProperty("organizationName").GetString());
        Assert.Equal(3500m, result.GetProperty("monthlyHouseholdIncome").GetDecimal());
        Assert.Equal(875m, result.GetProperty("perCapitaIncome").GetDecimal());
        Assert.Equal(JsonValueKind.Array, result.GetProperty("matchedSchemes").ValueKind);
    }

    [Fact]
    public async Task Search_matches_income_tier_and_marks_profile_criteria_for_login_verification()
    {
        string schemeName = $"Public income scheme {Guid.NewGuid():N}";
        await SeedPublicScheme(schemeName);

        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/public/v1/fas/search", new
        {
            organizationId = 1,
            monthlyHouseholdIncome = 3000m,
            householdMemberCount = 4
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement match = document.RootElement.GetProperty("data").GetProperty("matchedSchemes")
            .EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == schemeName);
        Assert.Equal("Income support", match.GetProperty("benefit").GetProperty("tierLabel").GetString());
        Assert.True(match.GetProperty("requiresLoginVerification").GetBoolean());
    }

    [Theory]
    [InlineData(0, 3500, 4)]
    [InlineData(1, -1, 4)]
    [InlineData(1, 3500, 0)]
    public async Task Search_rejects_invalid_required_fields(long schoolId, decimal income, int members)
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/public/v1/fas/search", new
        {
            organizationId = schoolId,
            monthlyHouseholdIncome = income,
            householdMemberCount = members
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("FAS.INVALID_PUBLIC_SEARCH", await response.Content.ReadAsStringAsync());
    }

    private async Task SeedPublicScheme(string name)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        DateOnly today = clock.TodayInSingapore();

        FasScheme scheme = FasScheme.CreateDraft(
            $"PUBLIC-{Guid.NewGuid():N}",
            $"PUBLIC-GRANT-{Guid.NewGuid():N}",
            name,
            "Public search integration scheme",
            today,
            today.AddMonths(1),
            1002,
            utcNow);
        scheme.Activate(1002, utcNow);
        db.Add(scheme);
        await db.SaveChangesAsync();

        FasTier tier = FasTier.Create(scheme.Id, "Income support", "PERCENTAGE", 80m, 1, utcNow);
        db.Add(tier);
        await db.SaveChangesAsync();

        FasTierCriteriaGroup group = FasTierCriteriaGroup.Create(tier.Id, 1, utcNow);
        db.Add(group);
        await db.SaveChangesAsync();

        db.Add(FasTierCriteria.Create(tier.Id, group.Id, "GHI", 0m, 4000m, null, 1, utcNow));
        db.Add(FasTierCriteria.Create(tier.Id, group.Id, "PCI", 0m, 1000m, null, 2, utcNow));
        db.Add(FasTierCriteria.Create(tier.Id, group.Id, "NATIONALITY", null, null, null, 3, utcNow));
        await db.SaveChangesAsync();
    }
}
