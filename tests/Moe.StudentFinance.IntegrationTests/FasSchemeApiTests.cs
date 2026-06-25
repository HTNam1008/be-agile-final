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
    public async Task Draft_can_be_loaded_and_edited_through_canonical_endpoint()
    {
        string suffix = NewSuffix();
        using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes/draft", PercentageRequest(suffix));
        await AssertStatus(HttpStatusCode.Created, created);
        long id = await ReadLong(created, "schemeId");

        using HttpResponseMessage updated = await _client.PutAsJsonAsync($"/api/admin/v1/fas/schemes/{id}", PercentageRequest(suffix, name: $"Edited FAS {suffix}"));
        await AssertStatus(HttpStatusCode.OK, updated);
        using HttpResponseMessage detail = await _client.GetAsync($"/api/admin/v1/fas/schemes/{id}");
        await AssertStatus(HttpStatusCode.OK, detail);
        string body = await detail.Content.ReadAsStringAsync();
        Assert.Contains($"Edited FAS {suffix}", body);
        Assert.Contains("\"status\":\"DRAFT\"", body);
    }

    [Fact]
    public async Task Active_scheme_cannot_be_edited_as_draft()
    {
        string suffix = NewSuffix();
        using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", PercentageRequest(suffix));
        await AssertStatus(HttpStatusCode.Created, created);
        long id = await ReadLong(created, "schemeId");

        using HttpResponseMessage updated = await _client.PutAsJsonAsync($"/api/admin/v1/fas/schemes/{id}", PercentageRequest(suffix, name: "Should not save"));
        await AssertStatus(HttpStatusCode.NotFound, updated);
        Assert.Contains("FAS.SCHEME_NOT_FOUND", await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Parent_nationality_and_account_type_round_trip_without_numeric_bounds()
    {
        string suffix = NewSuffix();
        var request = new
        {
            schemeCode = $"CATEGORICAL-{suffix}",
            grantCode = $"CATEGORICAL-GRANT-{suffix}",
            name = $"Categorical {suffix}",
            startDate = new DateOnly(2026, 1, 1),
            endDate = new DateOnly(2026, 12, 31),
            courseIds = Array.Empty<long>(),
            subsidyType = "PERCENTAGE",
            criteriaTemplate = new object[]
            {
                new { criteriaType = "PARENT_NATIONALITY", connectorToNext = "AND", displayOrder = 1 },
                new { criteriaType = "ACCOUNT_TYPE", connectorToNext = (string?)null, displayOrder = 2 }
            },
            tiers = new object[]
            {
                new { label = "Eligible", subsidyValue = 75m, displayOrder = 1, criteriaValues = new object[]
                {
                    new { displayOrder = 1, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "Vietnamese", "Singapore Citizen" } },
                    new { displayOrder = 2, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "EDUCATION_ACCOUNT" } }
                }}
            }
        };

        using HttpResponseMessage created = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", request);
        await AssertStatus(HttpStatusCode.Created, created);
        long id = await ReadLong(created, "schemeId");
        using HttpResponseMessage detail = await _client.GetAsync($"/api/admin/v1/fas/schemes/{id}");
        await AssertStatus(HttpStatusCode.OK, detail);
        string body = await detail.Content.ReadAsStringAsync();
        Assert.Contains("PARENT_NATIONALITY", body);
        Assert.Contains("Vietnamese", body);
        Assert.Contains("ACCOUNT_TYPE", body);
        Assert.Contains("EDUCATION_ACCOUNT", body);
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
            subsidyType = "Percentage",
            criteriaTemplate = Array.Empty<object>(),
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
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            enrollmentOpenAt = DateTime.UtcNow.AddMinutes(1),
            enrollmentCloseAt = DateTime.UtcNow.AddDays(14)
        });
        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatus(HttpStatusCode.Created, response);
        return await ReadLong(response, "courseId");
    }

    private static object PercentageRequest(string suffix, string? grantCode = null, string? name = null) => new
    {
        schemeCode = $"FAS-{suffix}",
        grantCode = grantCode ?? $"GRANT-{suffix}",
        name = name ?? $"FAS {suffix}",
        description = "Integration scheme",
        startDate = new DateOnly(2026, 1, 1),
        endDate = new DateOnly(2026, 12, 31),
        courseIds = Array.Empty<long>(),
        subsidyType = "PERCENTAGE",
        criteriaTemplate = new object[]
        {
            new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
            new { criteriaType = "NATIONALITY", connectorToNext = (string?)null, displayOrder = 2 }
        },
        tiers = new object[]
        {
            new
            {
                label = "Full",
                subsidyValue = 100m,
                displayOrder = 1,
                criteriaValues = new object[]
                {
                    new { displayOrder = 1, numberFrom = 13m, numberTo = 18m, nationalities = (string[]?)null },
                    new { displayOrder = 2, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "Singapore Citizen" } }
                }
            }
        }
    };

    private static object FixedRequest(string suffix, params long[] courseIds) => new
    {
        schemeCode = $"FIXED-{suffix}",
        grantCode = $"FIXED-GRANT-{suffix}",
        name = $"Fixed FAS {suffix}",
        startDate = new DateOnly(2026, 1, 1),
        endDate = new DateOnly(2026, 12, 31),
        courseIds,
        subsidyType = "FIXED",
        criteriaTemplate = new object[] { new { criteriaType = "AGE", connectorToNext = (string?)null, displayOrder = 1 } },
        tiers = new object[]
        {
            new
            {
                label = "Fixed Support",
                subsidyValue = 750m,
                displayOrder = 1,
                criteriaValues = new object[] { new { displayOrder = 1, numberFrom = 7m, numberTo = 25m, nationalities = (string[]?)null } }
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
