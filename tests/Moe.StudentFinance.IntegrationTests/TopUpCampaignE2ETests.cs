using System.Net.Http.Json;
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
            campaignCode = "TEST_FIXED_001",
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
        var recipientsPayload = new
        {
            mode = "ExplicitIds",
            filter = (object?)null,
            recipients = new[]
            {
                new { educationAccountId = 1001L },
                new { educationAccountId = 1002L }
            },
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
        var activatePayload = new { topUpCampaignId = campaignId, newStatusCode = "Active" };
        var activateResponse = await _client.PatchAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/status", activatePayload);
        if (!activateResponse.IsSuccessStatusCode)
        {
            var err = await activateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Activate failed: {activateResponse.StatusCode} - {err}");
        }

        // 5. Execute Run
        var executeResponse = await _client.PostAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/execute", null);
        if (!executeResponse.IsSuccessStatusCode)
        {
            var err = await executeResponse.Content.ReadAsStringAsync();
            throw new Exception($"Execute failed: {executeResponse.StatusCode} - {err}");
        }
        var runId = await executeResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(runId > 0);
    }

    [Fact]
    public async Task DynamicCampaign_FullLifecycle_Succeeds()
    {
        // 1. Create Campaign
        var createPayload = new
        {
            organizationId = 1,
            campaignCode = "TEST_DYN_001",
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
        var activatePayload = new { topUpCampaignId = campaignId, newStatusCode = "Active" };
        var activateResponse = await _client.PatchAsJsonAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/status", activatePayload);
        if (!activateResponse.IsSuccessStatusCode)
        {
            var err = await activateResponse.Content.ReadAsStringAsync();
            throw new Exception($"Activate failed: {activateResponse.StatusCode} - {err}");
        }

        // 5. Execute Run
        var executeResponse = await _client.PostAsync($"/api/admin/v1/top-up-campaigns/{campaignId}/execute", null);
        if (!executeResponse.IsSuccessStatusCode)
        {
            var err = await executeResponse.Content.ReadAsStringAsync();
            throw new Exception($"Execute failed: {executeResponse.StatusCode} - {err}");
        }
        var runId = await executeResponse.Content.ReadFromJsonAsync<long>();
        Assert.True(runId > 0);
    }
}
