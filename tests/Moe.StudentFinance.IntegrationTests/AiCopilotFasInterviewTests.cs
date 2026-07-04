using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;
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
        await SendFasMessage("999999", conversationId);
        await SendFasMessage("1", conversationId);
        await SendFasMessage("0", conversationId);

        JsonElement confirmation = await SendFasMessage("Foreigner", conversationId);
        Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());

        JsonElement completed = await SendFasMessage("yes", conversationId);

        Assert.Equal("MANUAL_FALLBACK", completed.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.Contains("could not find an eligible FAS scheme", completed.GetProperty("text").GetString());
        Assert.DoesNotContain(completed.GetProperty("cards").EnumerateArray(), x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION");
        Assert.Contains(completed.GetProperty("actions").EnumerateArray(), x => x.GetProperty("type").GetString() == "CONTACT_ADMIN_CENTER");
        Assert.True(completed.TryGetProperty("reviewRecordId", out JsonElement review) && review.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task Fas_interview_returns_typed_recommendation_and_form_patch_when_complete()
    {
        string schemeName = await CreateEligibleScheme();
        await AssertEligibilityHasMatches(schemeName);
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("No", conversationId);
        await SendFasMessage("3000", conversationId);
        await SendFasMessage("4", conversationId);
        JsonElement beforeNationality = await SendFasMessage("0", conversationId);
        Assert.Equal(3000m, Field(beforeNationality, "monthlyHouseholdIncome").GetProperty("value").GetDecimal());
        Assert.Equal(4, Field(beforeNationality, "householdMemberCount").GetProperty("value").GetInt32());
        Assert.Equal(0m, Field(beforeNationality, "otherMonthlyIncome").GetProperty("value").GetDecimal());

        JsonElement confirmation = await SendFasMessage("Singapore Citizen", conversationId);

        Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.DoesNotContain(confirmation.GetProperty("cards").EnumerateArray(), x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION");

        JsonElement completed = await SendFasMessage("yes", conversationId);

        Assert.Equal("GENERAL", completed.GetProperty("mode").GetString());
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
    public async Task Fas_interview_returns_multiple_ranked_scheme_matches()
    {
        string lowerScheme = await CreateEligibleScheme(label: "Partial", subsidyValue: 60m);
        string higherScheme = await CreateEligibleScheme(label: "Full", subsidyValue: 95m);
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("No", conversationId);
        await SendFasMessage("3000", conversationId);
        await SendFasMessage("4", conversationId);
        await SendFasMessage("0", conversationId);

        JsonElement confirmation = await SendFasMessage("Singaporean", conversationId);
        Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());

        JsonElement completed = await SendFasMessage("yes", conversationId);

        JsonElement data = completed.GetProperty("cards").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION").GetProperty("data");
        JsonElement[] matches = data.GetProperty("matchedSchemes").EnumerateArray().ToArray();
        Assert.Contains(matches, x => x.GetProperty("schemeName").GetString() == lowerScheme);
        Assert.Contains(matches, x => x.GetProperty("schemeName").GetString() == higherScheme);

        int lowerIndex = Array.FindIndex(matches, x => x.GetProperty("schemeName").GetString() == lowerScheme);
        int higherIndex = Array.FindIndex(matches, x => x.GetProperty("schemeName").GetString() == higherScheme);
        Assert.True(higherIndex >= 0 && lowerIndex >= 0 && higherIndex < lowerIndex,
            $"Expected higher subsidy scheme before lower subsidy scheme. Matches: {string.Join(", ", matches.Select(x => x.GetProperty("schemeName").GetString()))}");
        Assert.True(matches[higherIndex].GetProperty("recommendationRank").GetInt32() < matches[lowerIndex].GetProperty("recommendationRank").GetInt32());
        Assert.Contains("Best fit", matches[0].GetProperty("recommendationReason").GetString());
    }

    [Fact]
    public async Task Fas_interview_autofills_only_actionable_scheme_matches()
    {
        (string pendingScheme, long pendingId) = await CreateEligibleSchemeWithId(label: "Pending", subsidyValue: 100m);
        (string actionableScheme, long actionableId) = await CreateEligibleSchemeWithId(label: "Actionable", subsidyValue: 60m);
        try
        {
            await SubmitPendingApplication(pendingId);
            Guid conversationId = await StartFasInterview();
            await SendFasMessage("No", conversationId);
            await SendFasMessage("3000", conversationId);
            await SendFasMessage("4", conversationId);
            await SendFasMessage("0", conversationId);

            JsonElement confirmation = await SendFasMessage("Singaporean", conversationId);
            Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());

            JsonElement completed = await SendFasMessage("yes", conversationId);

            JsonElement data = completed.GetProperty("cards").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION").GetProperty("data");
            JsonElement[] matches = data.GetProperty("matchedSchemes").EnumerateArray().ToArray();
            JsonElement pendingMatch = matches.Single(x => x.GetProperty("schemeName").GetString() == pendingScheme);
            JsonElement actionableMatch = matches.Single(x => x.GetProperty("schemeName").GetString() == actionableScheme);
            Assert.True(pendingMatch.GetProperty("hasPendingApplication").GetBoolean());
            Assert.False(pendingMatch.GetProperty("canApply").GetBoolean());
            Assert.True(actionableMatch.GetProperty("canApply").GetBoolean());
            Assert.True(Array.FindIndex(matches, x => x.GetProperty("schemeName").GetString() == actionableScheme) <
                        Array.FindIndex(matches, x => x.GetProperty("schemeName").GetString() == pendingScheme));

            long[] patchIds = completed.GetProperty("interviewState").GetProperty("formPatch").GetProperty("schemes").GetProperty("recommendedSchemeIds")
                .EnumerateArray().Select(x => x.GetInt64()).ToArray();
            Assert.Contains(actionableId, patchIds);
            Assert.DoesNotContain(pendingId, patchIds);
        }
        finally
        {
            await DisableScheme(pendingId);
            await DisableScheme(actionableId);
        }
    }

    [Fact]
    public async Task Fas_interview_marks_mixed_subsidy_matches_as_review_required()
    {
        (string _, long percentageId) = await CreateEligibleSchemeWithId(label: "Percentage", subsidyValue: 90m, subsidyType: "PERCENTAGE");
        (string _, long fixedId) = await CreateEligibleSchemeWithId(label: "Fixed", subsidyValue: 500m, subsidyType: "FIXED");
        try
        {
            Guid conversationId = await StartFasInterview();
            await SendFasMessage("No", conversationId);
            await SendFasMessage("3000", conversationId);
            await SendFasMessage("4", conversationId);
            await SendFasMessage("0", conversationId);

            JsonElement confirmation = await SendFasMessage("Singaporean", conversationId);
            Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());

            JsonElement completed = await SendFasMessage("yes", conversationId);

            JsonElement data = completed.GetProperty("cards").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "FAS_RECOMMENDATION").GetProperty("data");
            JsonElement[] matches = data.GetProperty("matchedSchemes").EnumerateArray().ToArray();
            Assert.Contains(matches, x => x.GetProperty("subsidyType").GetString() == "PERCENTAGE");
            Assert.Contains(matches, x => x.GetProperty("subsidyType").GetString() == "FIXED");
            Assert.False(data.GetProperty("isComparable").GetBoolean());
            Assert.Equal("REVIEW_REQUIRED", data.GetProperty("recommendationConfidence").GetString());
            Assert.All(matches, match =>
            {
                Assert.False(match.GetProperty("isComparable").GetBoolean());
                Assert.Equal("REVIEW_REQUIRED", match.GetProperty("recommendationConfidence").GetString());
                Assert.Contains("not directly comparable", match.GetProperty("recommendationReason").GetString());
            });
        }
        finally
        {
            await DisableScheme(percentageId);
            await DisableScheme(fixedId);
        }
    }


    [Fact]
    public async Task Fas_interview_welfare_home_path_prepares_scheme_patch()
    {
        await CreateEligibleScheme();
        Guid conversationId = await StartFasInterview();
        await SendFasMessage("Yes", conversationId);

        JsonElement confirmation = await SendFasMessage("Singapore Citizen", conversationId);
        Assert.Equal("CONFIRMING", confirmation.GetProperty("interviewState").GetProperty("status").GetString());

        JsonElement completed = await SendFasMessage("yes", conversationId);

        Assert.Equal("COMPLETE", completed.GetProperty("interviewState").GetProperty("status").GetString());
        Assert.Contains("welfare-home status", completed.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
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

    private async Task AssertEligibilityHasMatches(string expectedSchemeName)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/fas/eligibility/check");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new
        {
            monthlyHouseholdIncome = 3000m,
            householdMemberCount = 4,
            otherMonthlyIncome = 0m,
            parentNationalities = new[] { "Singapore Citizen" }
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        await AssertStatus(HttpStatusCode.OK, response);
        JsonElement data = await ReadData(response);
        JsonElement[] matches = data.GetProperty("matchedSchemes").EnumerateArray().ToArray();
        if (!matches.Any(match => match.GetProperty("schemeName").GetString() == expectedSchemeName))
            Assert.Fail($"Expected eligibility match for {expectedSchemeName}. Response: {data}");
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
            startDate = SingaporeBusinessDay.FromUtc(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            courseIds = new[] { courseId },
            subsidyType = "PERCENTAGE",
            criteriaGroups = new object[]
            {
                new
                {
                    displayOrder = 1,
                    criteria = new object[]
                    {
                        new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                        new { criteriaType = "GHI", connectorToNext = "AND", displayOrder = 2 },
                        new { criteriaType = "PCI", connectorToNext = (string?)null, displayOrder = 3 }
                    }
                }
            },
            criteriaTemplate = new object[]
            {
                new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                new { criteriaType = "GHI", connectorToNext = "AND", displayOrder = 2 },
                new { criteriaType = "PCI", connectorToNext = (string?)null, displayOrder = 3 }
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
                        new { displayOrder = 1, numberFrom = 16m, numberTo = 30m, nationalities = (string[]?)null },
                        new { displayOrder = 2, numberFrom = 0m, numberTo = 10000m, nationalities = (string[]?)null },
                        new { displayOrder = 3, numberFrom = 0m, numberTo = maxPci, nationalities = (string[]?)null }
                    }
                }
            }
        });

        await AssertStatus(HttpStatusCode.Created, response);
    }

    private async Task<string> CreateEligibleScheme(string label = "Full", decimal subsidyValue = 100m, string subsidyType = "PERCENTAGE")
        => (await CreateEligibleSchemeWithId(label, subsidyValue, subsidyType)).Name;

    private async Task<(string Name, long Id)> CreateEligibleSchemeWithId(string label = "Full", decimal subsidyValue = 100m, string subsidyType = "PERCENTAGE")
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string schemeName = $"AI FAS {label} {suffix}";
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
            name = schemeName,
            description = "AI copilot integration scheme",
            startDate = SingaporeBusinessDay.FromUtc(DateTime.UtcNow),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            courseIds = new[] { courseId },
            subsidyType,
            criteriaGroups = new object[]
            {
                new
                {
                    displayOrder = 1,
                    criteria = new object[]
                    {
                        new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                        new { criteriaType = "GHI", connectorToNext = "AND", displayOrder = 2 },
                        new { criteriaType = "PCI", connectorToNext = (string?)null, displayOrder = 3 }
                    }
                }
            },
            criteriaTemplate = new object[]
            {
                new { criteriaType = "AGE", connectorToNext = "AND", displayOrder = 1 },
                new { criteriaType = "GHI", connectorToNext = "AND", displayOrder = 2 },
                new { criteriaType = "PCI", connectorToNext = (string?)null, displayOrder = 3 }
            },
            tiers = new object[]
            {
                new
                {
                    label,
                    subsidyValue,
                    displayOrder = 1,
                    criteriaValues = new object[]
                    {
                        new { displayOrder = 1, numberFrom = 16m, numberTo = 30m, nationalities = (string[]?)null },
                        new { displayOrder = 2, numberFrom = 0m, numberTo = 10000m, nationalities = (string[]?)null },
                        new { displayOrder = 3, numberFrom = 0m, numberTo = 1000m, nationalities = (string[]?)null }
                    }
                }
            }
        });

        await AssertStatus(HttpStatusCode.Created, response);
        JsonElement responseData = await ReadData(response);
        return (schemeName, responseData.GetProperty("schemeId").GetInt64());
    }

    private async Task DisableScheme(long schemeId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, $"/api/admin/v1/fas/schemes/{schemeId}/disable");
        request.Headers.Add("X-Test-Role", "HQ_ADMIN");
        using HttpResponseMessage response = await _client.SendAsync(request);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.UnprocessableEntity or HttpStatusCode.NotFound))
            Assert.Fail($"Scheme cleanup failed: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task SubmitPendingApplication(long schemeId)
    {
        await EnsureStudentProfileCanSubmitApplication();

        using HttpRequestMessage draftRequest = new(HttpMethod.Post, "/api/eservice/v1/fas/applications/draft");
        draftRequest.Headers.Add("X-Test-PersonId", "2101");
        draftRequest.Content = JsonContent.Create(new { schemeIds = new[] { schemeId } });
        using HttpResponseMessage draftResponse = await _client.SendAsync(draftRequest);
        await AssertStatus(HttpStatusCode.OK, draftResponse);
        long applicationId = (await ReadData(draftResponse)).GetProperty("id").GetInt64();
        await MarkApplicationSchemePending(applicationId, schemeId);
    }

    private async Task EnsureStudentProfileCanSubmitApplication()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Person person = await db.Set<Person>().FindAsync(2101L)
            ?? throw new InvalidOperationException("Integration student 2101 was not seeded.");
        SetPrivateProperty(person, nameof(Person.IdentityNumberMasked), "S7000001A");
        SetPrivateProperty(person, nameof(Person.OfficialMobile), "+6591000001");
        SetPrivateProperty(person, nameof(Person.PreferredMobile), "+6591000001");
        SetPrivateProperty(person, nameof(Person.OfficialAddress), "1 Integration Avenue, Singapore 100001");
        SetPrivateProperty(person, nameof(Person.PreferredAddress), "1 Integration Avenue, Singapore 100001");
        SetPrivateProperty(person, nameof(Person.OfficialEmail), "student.one@example.test");
        SetPrivateProperty(person, nameof(Person.PreferredEmail), "student.one@example.test");
        await db.SaveChangesAsync();
    }

    private async Task MarkApplicationSchemePending(long applicationId, long schemeId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        Type schemeSelectionType = typeof(Moe.Modules.FasPayment.Application.StudentApplications.StudentFasApplicationService)
            .Assembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasApplicationScheme", throwOnError: true)!;
        object selection = await SetFor(db, schemeSelectionType)
            .Cast<object>()
            .SingleAsync(x =>
                EF.Property<long>(x, "FasApplicationId") == applicationId &&
                EF.Property<long>(x, "FasSchemeId") == schemeId);
        SetPrivateProperty(selection, "StatusCode", "PENDING");
        await db.SaveChangesAsync();
    }

    private static JsonElement Field(JsonElement response, string name)
        => response.GetProperty("interviewState").GetProperty("fields").EnumerateArray()
            .Single(x => x.GetProperty("name").GetString() == name);

    private static void SetPrivateProperty<T>(object entity, string propertyName, T value)
        => entity.GetType().GetProperty(propertyName)!.SetValue(entity, value);

    private static IQueryable SetFor(DbContext db, Type entityType)
        => (IQueryable)typeof(DbContext)
            .GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
            .MakeGenericMethod(entityType)
            .Invoke(db, null)!;

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
