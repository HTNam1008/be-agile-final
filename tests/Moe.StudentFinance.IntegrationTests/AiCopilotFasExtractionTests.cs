using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotFasExtractionTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("Yes")]
    [InlineData("yes")]
    [InlineData("y")]
    [InlineData("I live in an approved welfare home")]
    public async Task Welfare_home_yes_variations_are_accepted(string answer)
    {
        Guid cid = await StartInterview();
        JsonElement response = await SendFas(answer, cid);
        AssertWelfareStatus(response, true);
        Assert.Equal("COMPLETE", GetInterviewStatus(response));
    }

    [Theory]
    [InlineData("No")]
    [InlineData("no")]
    [InlineData("n")]
    [InlineData("Not in a welfare home")]
    [InlineData("Not in an approved welfare home")]
    [InlineData("I do not reside in one")]
    public async Task Welfare_home_no_variations_are_accepted(string answer)
    {
        Guid cid = await StartInterview();
        JsonElement response = await SendFas(answer, cid);
        AssertWelfareStatus(response, false);
        Assert.Equal("COLLECTING", GetInterviewStatus(response));
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("I am not sure")]
    [InlineData("What is that?")]
    public async Task Welfare_home_ambiguous_triggers_clarification(string answer)
    {
        Guid cid = await StartInterview();
        JsonElement response = await SendFas(answer, cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(response));
        Assert.Contains("yes or no", response.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("3200")]
    [InlineData("3.2k")]
    [InlineData("3,200")]
    [InlineData("3200.50")]
    [InlineData("my income is 2800")]
    public async Task Income_valid_values_are_accepted(string answer)
    {
        Guid cid = await StartNoWelfare();
        JsonElement response = await SendFas(answer, cid);
        JsonElement field = GetField(response, "monthlyHouseholdIncome");
        Assert.True(field.GetProperty("confirmed").GetBoolean());
    }

    [Fact]
    public async Task Income_with_k_suffix_is_multiplied()
    {
        Guid cid = await StartNoWelfare();
        JsonElement response = await SendFas("5k", cid);
        JsonElement field = GetField(response, "monthlyHouseholdIncome");
        Assert.True(field.GetProperty("confirmed").GetBoolean());
    }

    [Fact]
    public async Task Income_negative_is_rejected()
    {
        Guid cid = await StartNoWelfare();
        JsonElement response = await SendFas("-500", cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(response));
        Assert.Contains("valid", response.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Income_over_one_million_is_rejected()
    {
        Guid cid = await StartNoWelfare();
        JsonElement response = await SendFas("1000001", cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(response));
    }

    [Fact]
    public async Task Income_no_numbers_triggers_clarification()
    {
        Guid cid = await StartNoWelfare();
        JsonElement response = await SendFas("I do not know", cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(response));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("1")]
    [InlineData("30")]
    public async Task Household_count_valid_values_are_accepted(string answer)
    {
        Guid cid = await StartNoWelfare();
        await SendFas("3000", cid);
        JsonElement response = await SendFas(answer, cid);
        JsonElement field = GetField(response, "householdMemberCount");
        Assert.True(field.GetProperty("confirmed").GetBoolean());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("31")]
    [InlineData("four")]
    public async Task Household_count_invalid_triggers_clarification(string answer)
    {
        Guid cid = await StartNoWelfare();
        await SendFas("3000", cid);
        JsonElement response = await SendFas(answer, cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(response));
    }

    [Theory]
    [InlineData("Singapore Citizen", "Singapore Citizen")]
    [InlineData("Singaporean", "Singapore Citizen")]
    [InlineData("Singapore", "Singapore Citizen")]
    [InlineData("SG", "Singapore Citizen")]
    [InlineData("singapore citizen", "Singapore Citizen")]
    public async Task Nationality_singapore_variations_normalize_correctly(string answer, string expected)
    {
        Guid cid = await StartNoWelfare();
        await SendFas("3000", cid);
        await SendFas("4", cid);
        JsonElement response = await SendFas(answer, cid);
        JsonElement field = GetField(response, "parentNationalities");
        Assert.True(field.GetProperty("confirmed").GetBoolean());
        Assert.Equal(expected, field.GetProperty("value")[0].GetString());
    }

    [Fact]
    public async Task Max_clarification_attempts_triggers_manual_fallback()
    {
        Guid cid = await StartInterview();
        // Answer welfare home as "No" to proceed to income
        await SendFas("No", cid);
        // Give ambiguous income answers twice -> manual fallback
        JsonElement first = await SendFas("I do not know", cid);
        Assert.Equal("CLARIFYING", GetInterviewStatus(first));
        JsonElement second = await SendFas("Still not sure", cid);
        Assert.Equal("MANUAL_FALLBACK", GetInterviewStatus(second));
        Assert.Contains("Please continue in the FAS form", second.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Welfare_home_true_skips_income_questions()
    {
        Guid cid = await StartInterview();
        JsonElement response = await SendFas("Yes", cid);
        Assert.Equal("COMPLETE", GetInterviewStatus(response));
        JsonElement fields = response.GetProperty("interviewState").GetProperty("fields");
        // Income fields should be unconfirmed when welfare home is true
        foreach (JsonElement f in fields.EnumerateArray())
        {
            if (f.GetProperty("name").GetString() is "monthlyHouseholdIncome" or "householdMemberCount")
                Assert.False(f.GetProperty("confirmed").GetBoolean());
        }
    }

    [Fact]
    public async Task Form_patch_includes_provenance_dictionary()
    {
        await CreateEligibleScheme();
        Guid cid = await StartInterview();
        await SendFas("No", cid);
        await SendFas("3000", cid);
        await SendFas("4", cid);
        JsonElement completed = await SendFas("Singaporean", cid);

        JsonElement patch = completed.GetProperty("interviewState").GetProperty("formPatch");
        Assert.True(patch.TryGetProperty("provenance", out JsonElement prov));
        Assert.Equal("AI_CONFIRMED", prov.GetProperty("isWelfareHomeResident").GetString());
        Assert.Equal("AI_CONFIRMED", prov.GetProperty("monthlyHouseholdIncome").GetString());
        Assert.Equal("AI_CONFIRMED", prov.GetProperty("householdMemberCount").GetString());
        Assert.Equal("AI_CONFIRMED", prov.GetProperty("parentNationalities").GetString());
    }

    [Fact]
    public async Task Form_patch_provenance_shows_unmapped_before_answering()
    {
        Guid cid = await StartInterview();
        JsonElement started = await SendFas("No", cid);
        JsonElement patch = started.GetProperty("interviewState").GetProperty("formPatch");
        Assert.True(patch.TryGetProperty("provenance", out JsonElement prov));
        Assert.Equal("AI_CONFIRMED", prov.GetProperty("isWelfareHomeResident").GetString());
        Assert.Equal("UNMAPPED", prov.GetProperty("monthlyHouseholdIncome").GetString());
        Assert.Equal("UNMAPPED", prov.GetProperty("householdMemberCount").GetString());
        Assert.Equal("UNMAPPED", prov.GetProperty("parentNationalities").GetString());
    }

    private async Task<Guid> StartInterview()
    {
        JsonElement started = await SendFas("Can you check my FAS eligibility?", null);
        Assert.Equal("FAS_INTERVIEW", started.GetProperty("mode").GetString());
        return started.GetProperty("conversationId").GetGuid();
    }

    private async Task<Guid> StartNoWelfare()
    {
        Guid cid = await StartInterview();
        await SendFas("No", cid);
        return cid;
    }

    private async Task<JsonElement> SendFas(string message, Guid? conversationId)
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
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Expected 200, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        return root.TryGetProperty("data", out JsonElement data) ? data : root;
    }

    private async Task CreateEligibleScheme()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/admin/v1/fas/schemes", new
        {
            schemeCode = $"AI-FAS-{suffix}",
            grantCode = $"AI-FAS-GRANT-{suffix}",
            name = $"AI FAS {suffix}",
            description = "AI copilot integration scheme",
            startDate = new DateOnly(2026, 1, 1),
            endDate = new DateOnly(2026, 12, 31),
            courseIds = Array.Empty<long>(),
            subsidyType = "PERCENTAGE",
            criteriaTemplate = new object[]
            {
                new { criteriaType = "PCI", connectorToNext = (string?)null, displayOrder = 1 }
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
                        new { displayOrder = 1, numberFrom = 0m, numberTo = 1000m, nationalities = (string[]?)null }
                    }
                }
            }
        });
        if (response.StatusCode != HttpStatusCode.Created)
            Assert.Fail($"Scheme creation failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private static string GetInterviewStatus(JsonElement response)
        => response.GetProperty("interviewState").GetProperty("status").GetString()!;

    private static void AssertWelfareStatus(JsonElement response, bool expected)
    {
        JsonElement field = GetField(response, "isWelfareHomeResident");
        Assert.Equal(expected, field.GetProperty("value").GetBoolean());
        Assert.True(field.GetProperty("confirmed").GetBoolean());
    }

    private static JsonElement GetField(JsonElement response, string name)
        => response.GetProperty("interviewState").GetProperty("fields").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == name);
}
