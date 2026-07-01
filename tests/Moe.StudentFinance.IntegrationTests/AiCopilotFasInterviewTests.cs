using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotFasInterviewTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Fas_interview_clarifies_ambiguous_amounts_instead_of_confirming_them()
    {
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("No", conversationId);

        JsonElement ambiguous = await SendFasMessage("It is between 3000 and 4000.", conversationId);

        Assert.Equal("FAS_INTERVIEW", ambiguous.GetProperty("mode").GetString());
        Assert.Equal("CLARIFYING", ambiguous.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.Contains("more than one amount", ambiguous.GetProperty("text").GetString());
        JsonElement incomeField = Field(ambiguous, "monthlyHouseholdIncome");
        Assert.False(incomeField.GetProperty("confirmed").GetBoolean());
    }

    [Fact]
    public async Task Fas_interview_falls_back_when_no_eligible_schemes()
    {
        await CreateSchemeWithPciCap(500);
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("No", conversationId);
        await SendFasMessage("3000", conversationId);
        await SendFasMessage("4", conversationId);
        await SendFasMessage("0", conversationId);

        JsonElement completed = await SendFasMessage("Foreigner", conversationId);

        Assert.Equal("MANUAL_FALLBACK", completed.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.Contains("could not find an eligible FAS scheme", completed.GetProperty("text").GetString());
        Assert.DoesNotContain(completed.GetProperty("cards").EnumerateArray(), x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION");
        Assert.Contains(completed.GetProperty("actions").EnumerateArray(), x => x.GetProperty("type").GetString() == "CONTACT_ADMIN_CENTER");
        Assert.True(completed.TryGetProperty("reviewRecordId", out JsonElement review) && review.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task Fas_interview_returns_typed_recommendation_and_form_patch_when_complete()
    {
        await CreateEligibleScheme();
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("No", conversationId);
        await SendFasMessage("3000", conversationId);
        await SendFasMessage("4", conversationId);
        await SendFasMessage("0", conversationId);

        JsonElement completed = await SendFasMessage("Singaporean", conversationId);

        Assert.Equal("COMPLETE", completed.GetProperty("interviewState").GetProperty("status").GetString());
        JsonElement card = completed.GetProperty("cards").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION");
        JsonElement data = card.GetProperty("data");
        Assert.Equal(750m, data.GetProperty("perCapitaIncome").GetDecimal());
        Assert.Equal("Full", data.GetProperty("recommendedTierLabel").GetString());
        Assert.NotEmpty(data.GetProperty("confirmedFacts").EnumerateArray());

        JsonElement patch = completed.GetProperty("interviewState").GetProperty("formPatch");
        Assert.False(patch.GetProperty("income").GetProperty("isWelfareHomeResident").GetBoolean());
        Assert.Equal(3000m, patch.GetProperty("income").GetProperty("monthlyHouseholdIncome").GetDecimal());
        Assert.Equal(4, patch.GetProperty("income").GetProperty("householdMemberCount").GetInt32());
        Assert.Equal(0m, patch.GetProperty("income").GetProperty("otherMonthlyIncome").GetDecimal());
        Assert.Equal("Singapore Citizen", patch.GetProperty("particulars").GetProperty("parentNationalities")[0].GetString());
        Assert.True(patch.GetProperty("schemes").GetProperty("recommendedSchemeIds").GetArrayLength() > 0);
        Assert.Contains("AI FAS", patch.GetProperty("schemes").GetProperty("recommendedSchemeNames")[0].GetString());

        Assert.Contains(completed.GetProperty("actions").EnumerateArray(), x => x.GetProperty("type").GetString() == "APPLY_FAS_PATCH");
        JsonElement openAction = completed.GetProperty("actions").EnumerateArray().Single(x => x.GetProperty("label").GetString() == "Open FAS application");
        Assert.True(openAction.GetProperty("payload").GetProperty("schemes").GetProperty("recommendedSchemeIds").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Fas_interview_welfare_home_path_prepares_scheme_patch()
    {
        await CreateEligibleScheme();
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("Yes", conversationId);

        JsonElement completed = await SendFasMessage("Singapore Citizen", conversationId);

        Assert.Equal("COMPLETE", completed.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.Contains("prepared", completed.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
        JsonElement patch = completed.GetProperty("interviewState").GetProperty("formPatch");
        Assert.True(patch.GetProperty("income").GetProperty("isWelfareHomeResident").GetBoolean());
        Assert.Equal("Singapore Citizen", patch.GetProperty("particulars").GetProperty("parentNationalities")[0].GetString());
        Assert.True(patch.GetProperty("schemes").GetProperty("recommendedSchemeIds").GetArrayLength() > 0);
        Assert.Equal("AI_CONFIRMED", patch.GetProperty("meta").GetProperty("schemeIds").GetProperty("provenance").GetString());
        Assert.Contains(completed.GetProperty("actions").EnumerateArray(), x => x.GetProperty("type").GetString() == "APPLY_FAS_PATCH");
        JsonElement openAction = completed.GetProperty("actions").EnumerateArray().Single(x => x.GetProperty("label").GetString() == "Open FAS application");
        Assert.True(openAction.GetProperty("payload").GetProperty("schemes").GetProperty("recommendedSchemeIds").GetArrayLength() > 0);
    }

    private async Task<Guid> StartFasInterview()
    {
        JsonElement started = await SendFasMessage("Can you check my FAS eligibility?", null);
        Assert.Equal("FAS_INTERVIEW", started.GetProperty("mode").GetString());
        Assert.Contains("MOE record facts", started.GetProperty("text").GetString());
        Assert.Contains("I still need", started.GetProperty("text").GetString());
        Assert.Contains("Are you currently residing in an approved welfare home? Please answer yes or no.", started.GetProperty("text").GetString());
        Assert.Equal("isWelfareHomeResident", started.GetProperty("interviewState").GetProperty("missingFields")[0].GetString());
        return started.GetProperty("conversationId").GetGuid();
    }

    private async Task<JsonElement> SendFasMessage(string message, Guid? conversationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message,
            pageContext = new { domain = "FAS", surface = "FAS", path = "/portal/fas" }
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatus(HttpStatusCode.OK, response);
        return await ReadData(response);
    }

    private async Task CreateSchemeWithPciCap(decimal maxPci)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpRequestMessage courseRequest = new(HttpMethod.Post, "/api/admin/v1/courses");
        courseRequest.Headers.Add("X-Test-Role", "HQ_ADMIN");
        courseRequest.Content = JsonContent.Create(new
        {
            organizationId = 1,
            courseCode = $"FAS-{suffix}",
            courseName = $"FAS Course {suffix}",
            description = "FAS integration course",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            enrollmentOpenAt = DateTime.UtcNow.AddMinutes(-5),
            enrollmentCloseAt = DateTime.UtcNow.AddDays(14)
        });
        using HttpResponseMessage courseResponse = await _client.SendAsync(courseRequest);
        if (courseResponse.StatusCode != HttpStatusCode.Created)
            Assert.Fail($"Course creation failed: {courseResponse.StatusCode}");
        
        using JsonDocument json = JsonDocument.Parse(await courseResponse.Content.ReadAsStringAsync());
        long courseId = json.RootElement.GetProperty("data").GetProperty("courseId").GetInt64();

        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", new
        {
            schemeCode = $"AI-FAS-NO-{suffix}",
            grantCode = $"AI-FAS-NO-GRANT-{suffix}",
            name = $"AI FAS No Match {suffix}",
            description = "AI copilot no-match scheme",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            courseIds = Array.Empty<long>(),
            subsidyType = "PERCENTAGE",
            criteriaTemplate = new object[]
            {
                new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                new { criteriaType = "PCI", connectorToNext = "AND", displayOrder = 2 },
                new { criteriaType = "PARENT_NATIONALITY", connectorToNext = "AND", displayOrder = 3 },
                new { criteriaType = "ACCOUNT_TYPE", connectorToNext = (string?)null, displayOrder = 4 }
            },
            tiers = new object[]
            {
                new
                {
                    label = "Limited",
                    subsidyValue = 50m,
                    displayOrder = 1,
                    criteriaValues = new object[]
                    {
                        new { displayOrder = 1, numberFrom = 13m, numberTo = 25m, nationalities = (string[]?)null },
                        new { displayOrder = 2, numberFrom = 0m, numberTo = maxPci, nationalities = (string[]?)null },
                        new { displayOrder = 3, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "Singapore Citizen" } },
                        new { displayOrder = 4, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "EDUCATION_ACCOUNT" } }
                    }
                }
            }
        });

        await AssertStatus(HttpStatusCode.Created, response);
    }

    private async Task CreateEligibleScheme()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpRequestMessage courseRequest = new(HttpMethod.Post, "/api/admin/v1/courses");
        courseRequest.Headers.Add("X-Test-Role", "HQ_ADMIN");
        courseRequest.Content = JsonContent.Create(new
        {
            organizationId = 1,
            courseCode = $"FAS-{suffix}",
            courseName = $"FAS Course {suffix}",
            description = "FAS integration course",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)),
            enrollmentOpenAt = DateTime.UtcNow.AddMinutes(-5),
            enrollmentCloseAt = DateTime.UtcNow.AddDays(14)
        });
        using HttpResponseMessage courseResponse = await _client.SendAsync(courseRequest);
        if (courseResponse.StatusCode != HttpStatusCode.Created)
            Assert.Fail($"Course creation failed: {courseResponse.StatusCode}");
        
        using JsonDocument json = JsonDocument.Parse(await courseResponse.Content.ReadAsStringAsync());
        long courseId = json.RootElement.GetProperty("data").GetProperty("courseId").GetInt64();

        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", new
        {
            schemeCode = $"AI-FAS-{suffix}",
            grantCode = $"AI-FAS-GRANT-{suffix}",
            name = $"AI FAS {suffix}",
            description = "AI copilot integration scheme",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            courseIds = Array.Empty<long>(),
            subsidyType = "PERCENTAGE",
            criteriaTemplate = new object[]
            {
                new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                new { criteriaType = "PCI", connectorToNext = "AND", displayOrder = 2 },
                new { criteriaType = "PARENT_NATIONALITY", connectorToNext = "AND", displayOrder = 3 },
                new { criteriaType = "ACCOUNT_TYPE", connectorToNext = (string?)null, displayOrder = 4 }
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
                        new { displayOrder = 1, numberFrom = 13m, numberTo = 25m, nationalities = (string[]?)null },
                        new { displayOrder = 2, numberFrom = 0m, numberTo = 1000m, nationalities = (string[]?)null },
                        new { displayOrder = 3, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "Singapore Citizen" } },
                        new { displayOrder = 4, numberFrom = (decimal?)null, numberTo = (decimal?)null, nationalities = new[] { "EDUCATION_ACCOUNT" } }
                    }
                }
            }
        });

        await AssertStatus(HttpStatusCode.Created, response);
    }

    private static JsonElement Field(JsonElement response, string name)
        => response.GetProperty("interviewState").GetProperty("fields").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == name);

    private static async Task<JsonElement> ReadData(HttpResponseMessage response)
    {
        JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = document.RootElement.Clone();
        return root.TryGetProperty("data", out JsonElement data) ? data : root;
    }

    private static async Task AssertStatus(HttpStatusCode expected, HttpResponseMessage response)
    {
        if (response.StatusCode != expected)
            Assert.Fail($"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
