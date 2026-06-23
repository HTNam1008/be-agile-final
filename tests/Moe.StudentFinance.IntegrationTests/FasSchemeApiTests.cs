using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class FasSchemeApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_list_and_detail_round_trip_global_percentage_scheme()
    {
        string suffix = NewSuffix();
        object request = PercentageRequest(suffix);

        using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", request);

        await AssertStatus(HttpStatusCode.Created, created);
        long id = await ReadLong(created, "schemeId");
        using HttpResponseMessage listed = await _client.GetAsync($"/api/admin/v1/fas/schemes?search={suffix}&status=ACTIVE");
        await AssertStatus(HttpStatusCode.OK, listed);
        string listBody = await listed.Content.ReadAsStringAsync();
        Assert.Contains($"FAS-{suffix}", listBody);
        Assert.Contains($"GRANT-{suffix}", listBody);

        using HttpResponseMessage detail = await _client.GetAsync($"/api/admin/v1/fas/schemes/{id}");
        await AssertStatus(HttpStatusCode.OK, detail);
        string detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains("Singapore Citizen", detailBody);
        Assert.Contains("PERCENTAGE", detailBody);
        Assert.Contains("\"courseIds\":[]", detailBody);
    }

    [Fact]
    public async Task Course_restricted_fixed_scheme_returns_numeric_course_ids()
    {
        long courseId = await CreateCourse();
        string suffix = NewSuffix();
        object request = FixedRequest(suffix, courseId);

        using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", request);
        await AssertStatus(HttpStatusCode.Created, created);
        long schemeId = await ReadLong(created, "schemeId");

        using HttpResponseMessage detail = await _client.GetAsync($"/api/admin/v1/fas/schemes/{schemeId}");
        await AssertStatus(HttpStatusCode.OK, detail);
        string body = await detail.Content.ReadAsStringAsync();
        Assert.Contains("FIXED", body);
        Assert.Contains($"\"courseIds\":[{courseId}]", body);
    }

    [Fact]
    public async Task FAS_validation_failures_are_422_not_400_or_500()
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", new
        {
            schemeCode = "",
            grantCode = "",
            name = "",
            startDate = "",
            endDate = "",
            courseIds = Array.Empty<long>(),
            tiers = Array.Empty<object>()
        });

        await AssertStatus(HttpStatusCode.UnprocessableEntity, response);
        Assert.DoesNotContain("UNEXPECTED_ERROR", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_list_status_is_422()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/admin/v1/fas/schemes?status=PUBLISHED");
        await AssertStatus(HttpStatusCode.UnprocessableEntity, response);
    }

    [Fact]
    public async Task Duplicate_scheme_and_grant_codes_have_stable_422_errors()
    {
        string suffix = NewSuffix();
        using HttpResponseMessage first = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", PercentageRequest(suffix));
        await AssertStatus(HttpStatusCode.Created, first);

        using HttpResponseMessage duplicateScheme = await _client.PostAsJsonAsync(
            "/api/admin/v1/fas/schemes",
            PercentageRequest(suffix, grantCode: $"OTHER-{suffix}"));
        await AssertStatus(HttpStatusCode.UnprocessableEntity, duplicateScheme);
        Assert.Contains("FAS.DUPLICATE_SCHEME_CODE", await duplicateScheme.Content.ReadAsStringAsync());

        using HttpResponseMessage duplicateGrant = await _client.PostAsJsonAsync(
            "/api/admin/v1/fas/schemes",
            PercentageRequest($"OTHER-{suffix}", grantCode: $"GRANT-{suffix}"));
        await AssertStatus(HttpStatusCode.UnprocessableEntity, duplicateGrant);
        Assert.Contains("FAS.DUPLICATE_GRANT_CODE", await duplicateGrant.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_courses_are_reported_together_as_422()
    {
        string suffix = NewSuffix();
        object request = FixedRequest(suffix, 8_888_881, 8_888_882);

        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", request);

        await AssertStatus(HttpStatusCode.UnprocessableEntity, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("FAS.UNKNOWN_COURSES", body);
        Assert.Contains("8888881", body);
        Assert.Contains("8888882", body);
    }

    [Fact]
    public async Task Missing_permission_is_403_and_anonymous_is_401()
    {
        using HttpRequestMessage forbiddenRequest = new(HttpMethod.Get, "/api/admin/v1/fas/schemes");
        forbiddenRequest.Headers.Add("X-Test-No-Fas-Permission", "true");
        using HttpResponseMessage forbidden = await _client.SendAsync(forbiddenRequest);
        await AssertStatus(HttpStatusCode.Forbidden, forbidden);

        using HttpRequestMessage anonymousRequest = new(HttpMethod.Get, "/api/admin/v1/fas/schemes");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "true");
        using HttpResponseMessage anonymous = await _client.SendAsync(anonymousRequest);
        await AssertStatus(HttpStatusCode.Unauthorized, anonymous);
    }

    [Fact]
    public async Task Missing_scheme_returns_not_found_without_internal_details()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/admin/v1/fas/schemes/9223372036854775807");
        await AssertStatus(HttpStatusCode.NotFound, response);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("FAS.SCHEME_NOT_FOUND", body);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", body);
    }

    private async Task<long> CreateCourse()
    {
        string suffix = NewSuffix();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/admin/v1/courses");
        request.Headers.Add("X-Test-Role", "HQ_ADMIN");
        request.Content = JsonContent.Create(new
        {
            organizationId = 1,
            courseCode = $"FAS-{suffix}",
            courseName = $"FAS Course {suffix}",
            description = "FAS integration course",
            startDate = new DateOnly(2027, 3, 1),
            endDate = new DateOnly(2027, 12, 31),
            enrollmentOpenAt = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAt = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatus(HttpStatusCode.Created, response);
        return await ReadLong(response, "courseId");
    }

    private static object PercentageRequest(string suffix, string? grantCode = null) => new
    {
        schemeCode = $"FAS-{suffix}",
        grantCode = grantCode ?? $"GRANT-{suffix}",
        name = $"FAS {suffix}",
        description = "Integration scheme",
        startDate = new DateOnly(2027, 1, 1),
        endDate = new DateOnly(2027, 12, 31),
        courseIds = Array.Empty<long>(),
        tiers = new object[]
        {
            new
            {
                label = "Full",
                subsidyType = "PERCENTAGE",
                subsidyValue = 100m,
                displayOrder = 1,
                criteria = new object[]
                {
                    new { criteriaType = "AGE", displayOrder = 1, numberFrom = 13m, numberTo = 18m, nationalities = (string[]?)null, connectorToNext = "AND" },
                    new { criteriaType = "NATIONALITY", displayOrder = 2, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "Singapore Citizen" }, connectorToNext = (string?)null }
                }
            }
        }
    };

    private static object FixedRequest(string suffix, params long[] courseIds) => new
    {
        schemeCode = $"FIXED-{suffix}",
        grantCode = $"FIXED-GRANT-{suffix}",
        name = $"Fixed FAS {suffix}",
        startDate = new DateOnly(2027, 1, 1),
        endDate = new DateOnly(2027, 12, 31),
        courseIds,
        tiers = new object[]
        {
            new
            {
                label = "Fixed Support",
                subsidyType = "FIXED",
                subsidyValue = 750m,
                displayOrder = 1,
                criteria = new object[] { new { criteriaType = "AGE", displayOrder = 1, numberFrom = 7m, numberTo = 25m, nationalities = (string[]?)null, connectorToNext = (string?)null } }
            }
        }
    };

    private static async Task<long> ReadLong(HttpResponseMessage response, string property)
    {
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty(property).GetInt64();
    }

    private static async Task AssertStatus(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
            Assert.Fail($"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
