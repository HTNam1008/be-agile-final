using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public class TopUpCampaignE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TopUpCampaignE2ETests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FixedCampaign_FullLifecycle_Succeeds()
    {
        // 1. Create Campaign
        var createPayload = new
        {
            organizationId = 1,
            campaignCode = $"TEST_FIXED_{Guid.NewGuid():N}",
            campaignName = "Integration Test Fixed",
            recipientModeCode = "FixedSelection",
            defaultTopUpAmount = 50.00m,
            reason = "E2E Testing",
            scheduleTypeCode = "OneTimeScheduled",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3).ToString("yyyy-MM-dd"),
            frequencyCode = "Monthly",
            frequencyInterval = 1,
            deliveryTypeCode = "FIXED_CONTRACT",
            maxTotalAmount = 150.00m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/admin/v1/top-up-campaigns", createPayload);
        if (!createResponse.IsSuccessStatusCode)
        {
            var err = await createResponse.Content.ReadAsStringAsync();
            throw new Exception($"Create failed: {createResponse.StatusCode} - {err}");
        }
        var campaignId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(campaignId > 0);

        var getResponse = await _client.GetAsync($"/api/admin/v1/top-up-campaigns/{campaignId}");
        if (!getResponse.IsSuccessStatusCode)
        {
            var err = await getResponse.Content.ReadAsStringAsync();
            throw new Exception($"GET failed: {getResponse.StatusCode} - {err}");
        }
        var getJson = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(getJson);
        Assert.Equal("FIXED_CONTRACT", doc.RootElement.GetProperty("deliveryTypeCode").GetString());
        Assert.Equal(150.00m, doc.RootElement.GetProperty("maxTotalAmount").GetDecimal());

        var updatePayload = new
        {
            campaignName = "Integration Test Fixed Updated",
            description = (string?)null,
            defaultTopUpAmount = 50.00m,
            reason = "E2E Testing",
            scheduleTypeCode = "Immediate",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd"),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(3).ToString("yyyy-MM-dd"),
            frequencyCode = "Monthly",
            frequencyInterval = 1,
            deliveryTypeCode = "FIXED_CONTRACT",
            maxTotalAmount = 150.00m,
            campaignVersion = 1
        };
        var updateResponse = await _client.PutAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}", updatePayload);
        if (!updateResponse.IsSuccessStatusCode)
        {
            var err = await updateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Update failed: {updateResponse.StatusCode} - {err}");
        }

        // 2. Upsert Recipients
        long[] educationAccountIds = await SearchEducationAccountIdsAsync();
        var recipientsPayload = new
        {
            mode = "ExplicitIds",
            filter = (object?)null,
            recipients = educationAccountIds.Select(educationAccountId => new
            {
                educationAccountId
            }).ToArray(),
            excludedEducationAccountIds = Array.Empty<long>()
        };
        var upsertResponse = await _client.PutAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/fixed-recipients", recipientsPayload);
        if (!upsertResponse.IsSuccessStatusCode)
        {
            var err = await upsertResponse.Content.ReadAsStringAsync();
            throw new Exception($"Upsert failed: {upsertResponse.StatusCode} - {err}");
        }

        // 3. Preview Campaign
        var previewPayload = new { pageNumber = 1, pageSize = 50 };
        var previewResponse = await _client.PostAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/preview", previewPayload);
        if (!previewResponse.IsSuccessStatusCode)
        {
            var err = await previewResponse.Content.ReadAsStringAsync();
            throw new Exception($"Preview failed: {previewResponse.StatusCode} - {err}");
        }

        // 4. Activate Campaign
        var activatePayload = new { topUpCampaignId = campaignId, newStatusCode = "ACTIVE" };
        var activateResponse = await _client.PatchAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/status", activatePayload);
        if (!activateResponse.IsSuccessStatusCode)
        {
            var err = await activateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Activate failed: {activateResponse.StatusCode} - {err}");
        }

        // 5. Request idempotent manual run and wait for worker completion.
        var idempotencyKey = $"fixed:{campaignId}:{Guid.NewGuid():N}";
        var runId = await RequestManualRunAsync(campaignId, idempotencyKey);
        var duplicateRunId = await RequestManualRunAsync(campaignId, idempotencyKey);
        Assert.Equal(runId, duplicateRunId);

        using JsonDocument summary = await WaitForRunSummaryAsync(runId);
        JsonElement data = summary.RootElement.GetProperty("data");
        Assert.Equal("COMPLETED", data.GetProperty("status").GetString());
        Assert.Equal(2, data.GetProperty("matchedCount").GetInt32());
        Assert.Equal(2, data.GetProperty("processedCount").GetInt32());
        Assert.Equal(2, data.GetProperty("succeededCount").GetInt32());
        Assert.Equal(0, data.GetProperty("failedCount").GetInt32());
        Assert.Equal(0, data.GetProperty("skippedCount").GetInt32());
    }

    [Fact]
    public async Task DynamicCampaign_FullLifecycle_Succeeds()
    {
        // 1. Create Campaign
        var createPayload = new
        {
            organizationId = 1,
            campaignCode = $"TEST_DYN_{Guid.NewGuid():N}",
            campaignName = "Integration Test Dynamic",
            recipientModeCode = "DynamicRules",
            defaultTopUpAmount = 75.00m,
            reason = "E2E Testing Dynamic",
            scheduleTypeCode = "Recurring",
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            endDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(12).ToString("yyyy-MM-dd"),
            frequencyCode = "Quarterly",
            frequencyInterval = 1,
            deliveryTypeCode = "CONDITIONAL_RECURRING",
            maxTotalAmount = 500.00m
        };
        var createResponse = await _client.PostAsJsonAsync("/api/admin/v1/top-up-campaigns", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var campaignId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(campaignId > 0);

        var getResponse = await _client.GetAsync($"/api/admin/v1/top-up-campaigns/{campaignId}");
        if (!getResponse.IsSuccessStatusCode)
        {
            var err = await getResponse.Content.ReadAsStringAsync();
            throw new Exception($"GET failed: {getResponse.StatusCode} - {err}");
        }
        var getJson = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(getJson);
        Assert.Equal("CONDITIONAL_RECURRING", doc.RootElement.GetProperty("deliveryTypeCode").GetString());
        Assert.Equal(500.00m, doc.RootElement.GetProperty("maxTotalAmount").GetDecimal());

        // 2. Upsert Rules
        var rulesPayload = new
        {
            topUpCampaignId = campaignId,
            rules = new[]
            {
                new { criterionCode = "Age", operatorCode = "GreaterThan", numericValueFrom = 7m }
            }
        };
        var upsertResponse = await _client.PutAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/rules", rulesPayload);
        if (!upsertResponse.IsSuccessStatusCode)
        {
            var err = await upsertResponse.Content.ReadAsStringAsync();
            throw new Exception($"Upsert failed: {upsertResponse.StatusCode} - {err}");
        }

        // 3. Preview Campaign
        var previewPayload = new { pageNumber = 1, pageSize = 50 };
        var previewResponse = await _client.PostAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/preview", previewPayload);
        if (!previewResponse.IsSuccessStatusCode)
        {
            var err = await previewResponse.Content.ReadAsStringAsync();
            throw new Exception($"Preview failed: {previewResponse.StatusCode} - {err}");
        }

        // 4. Activate Campaign
        var activatePayload = new { topUpCampaignId = campaignId, newStatusCode = "ACTIVE" };
        var activateResponse = await _client.PatchAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/status", activatePayload);
        if (!activateResponse.IsSuccessStatusCode)
        {
            var err = await activateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Activate failed: {activateResponse.StatusCode} - {err}");
        }

        // DynamicRules uses the same preview-gated execution path as fixed recipients.
        var idempotencyKey = $"dynamic:{campaignId}:{Guid.NewGuid():N}";
        var runId = await RequestManualRunAsync(campaignId, idempotencyKey);
        var duplicateRunId = await RequestManualRunAsync(campaignId, idempotencyKey);
        Assert.Equal(runId, duplicateRunId);

        using JsonDocument summary = await WaitForRunSummaryAsync(runId);
        JsonElement data = summary.RootElement.GetProperty("data");
        Assert.Equal("COMPLETED", data.GetProperty("status").GetString());
        Assert.True(data.GetProperty("matchedCount").GetInt32() > 0);
        Assert.Equal(
            data.GetProperty("matchedCount").GetInt32(),
            data.GetProperty("processedCount").GetInt32());
    }

    private async Task<long> RequestManualRunAsync(long campaignId, string idempotencyKey)
    {
        var request = new
        {
            idempotencyKey,
            note = "Integration test manual run"
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/top-up-campaigns/{campaignId}/runs",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Manual run request failed: {response.StatusCode} - {err}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(responseStream);
        return document.RootElement.GetProperty("data").GetProperty("runId").GetInt64();
    }

    private async Task<JsonDocument> WaitForRunSummaryAsync(long runId)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var response = await _client.GetAsync($"/api/admin/v1/top-up/runs/{runId}");
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Run summary failed: {response.StatusCode} - {err}");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var document = await JsonDocument.ParseAsync(responseStream);
            string? status = document.RootElement.GetProperty("data").GetProperty("status").GetString();
            if (status is "COMPLETED" or "PARTIAL" or "FAILED" or "CANCELLED")
            {
                return document;
            }

            document.Dispose();
            await Task.Delay(250);
        }

        throw new TimeoutException($"Run {runId} did not reach a terminal status.");
    }

    private async Task<long[]> SearchEducationAccountIdsAsync()
    {
        var searchResponse = await _client.GetAsync("/api/admin/v1/top-up/accounts/search?organizationId=1&accountStatusCode=ACTIVE&page=1&pageSize=2");
        if (!searchResponse.IsSuccessStatusCode)
        {
            var err = await searchResponse.Content.ReadAsStringAsync();
            throw new Exception($"Account search failed: {searchResponse.StatusCode} - {err}");
        }

        await using var responseStream = await searchResponse.Content.ReadAsStreamAsync();
        using var response = await JsonDocument.ParseAsync(responseStream);

        var data = response.RootElement.GetProperty("data");
        long[] ids = data.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("educationAccountId").GetInt64())
            .ToArray();

        Assert.True(ids.Length >= 2, $"Expected at least 2 active education accounts, found {ids.Length}.");
        return ids.Take(2).ToArray();
    }
}
