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
            scheduleTypeCode = "OneTimeScheduled"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/admin/v1/top-up-campaigns", createPayload);
        if (!createResponse.IsSuccessStatusCode)
        {
            var err = await createResponse.Content.ReadAsStringAsync();
            throw new Exception($"Create failed: {createResponse.StatusCode} - {err}");
        }
        var campaignId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(campaignId > 0);

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

        using JsonDocument summary = await WaitForRunSummaryAsync(campaignId, runId);
        JsonElement data = summary.RootElement.GetProperty("data");
        Assert.Equal("COMPLETED", data.GetProperty("runStatus").GetString());
        Assert.Equal(2, data.GetProperty("totalSelected").GetInt32());
        Assert.Equal(2, data.GetProperty("totalProcessed").GetInt32());
        Assert.Equal(2, data.GetProperty("totalSucceeded").GetInt32());
        Assert.Equal(0, data.GetProperty("totalFailed").GetInt32());
        Assert.Equal(0, data.GetProperty("totalSkipped").GetInt32());
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
            scheduleTypeCode = "OneTimeScheduled"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/admin/v1/top-up-campaigns", createPayload);
        createResponse.EnsureSuccessStatusCode();
        var campaignId = await createResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(campaignId > 0);

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

        // 5. Request idempotent manual run and wait for worker completion.
        var runId = await RequestManualRunAsync(campaignId, $"dynamic:{campaignId}:{Guid.NewGuid():N}");
        using JsonDocument summary = await WaitForRunSummaryAsync(campaignId, runId);
        JsonElement data = summary.RootElement.GetProperty("data");
        Assert.Equal("COMPLETED", data.GetProperty("runStatus").GetString());
        Assert.True(data.GetProperty("totalSucceeded").GetInt32() > 0);
    }

    private async Task<long> RequestManualRunAsync(long campaignId, string idempotencyKey)
    {
        var request = new
        {
            idempotencyKey,
            note = "Integration test manual run"
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/admin/v1/campaigns/{campaignId}/runs",
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

    private async Task<JsonDocument> WaitForRunSummaryAsync(long campaignId, long runId)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var response = await _client.GetAsync($"/api/admin/v1/campaigns/{campaignId}/runs/{runId}");
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                throw new Exception($"Run summary failed: {response.StatusCode} - {err}");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var document = await JsonDocument.ParseAsync(responseStream);
            string? status = document.RootElement.GetProperty("data").GetProperty("runStatus").GetString();
            if (status is "COMPLETED" or "PARTIAL" or "FAILED" or "CANCELLED")
            {
                return document;
            }

            document.Dispose();
            await Task.Delay(250);
        }

        throw new TimeoutException($"Run {runId} for campaign {campaignId} did not reach a terminal status.");
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
