using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class TopUpRunSummaryApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TopUpRunSummaryApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Run_Summary_Should_Return_Reconciled_Totals()
    {
        long runId = await SeedRunAsync(
            organizationId: 1,
            selected: 5,
            succeeded: 3,
            failed: 1,
            skipped: 1,
            totalCredited: 300m);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/admin/v1/top-up/runs/{runId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        RunSummaryEnvelope? payload =
            await response.Content.ReadFromJsonAsync<RunSummaryEnvelope>();

        Assert.NotNull(payload);
        Assert.Equal(runId, payload.Data.RunId);
        Assert.Equal(5, payload.Data.MatchedCount);
        Assert.Equal(5, payload.Data.ProcessedCount);
        Assert.Equal(3, payload.Data.SucceededCount);
        Assert.Equal(1, payload.Data.FailedCount);
        Assert.Equal(1, payload.Data.SkippedCount);
        Assert.Equal(300m, payload.Data.TotalCredited);
        Assert.Equal(TopUpRunStatusCodes.Partial, payload.Data.Status);

        string json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"note\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"idempotencyKey\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"ruleSnapshotJson\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_Summary_Should_Deny_Organization_Outside_Scope()
    {
        long runId = await SeedRunAsync(
            organizationId: 999,
            selected: 1,
            succeeded: 1,
            failed: 0,
            skipped: 0,
            totalCredited: 100m);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/admin/v1/top-up/runs/{runId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nested_Run_Summary_Should_Reject_Mismatched_Campaign()
    {
        long runId = await SeedRunAsync(
            organizationId: 1,
            selected: 1,
            succeeded: 1,
            failed: 0,
            skipped: 0,
            totalCredited: 100m);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/admin/v1/campaigns/999999/runs/{runId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<long> SeedRunAsync(
        long organizationId,
        int selected,
        int succeeded,
        int failed,
        int skipped,
        decimal totalCredited)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MoeDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        DateTime now = DateTime.UtcNow;
        string uniqueCode = $"B003-{Guid.NewGuid():N}";
        TopUpCampaign campaign = TopUpCampaign.Create(
            organizationId,
            uniqueCode,
            "B-003 integration campaign",
            null,
            "FIXED_SELECTION",
            100m,
            "B-003 integration test",
            "IMMEDIATE",
            DateOnly.FromDateTime(now),
            null,
            null,
            null,
            currentUserId: 1,
            now);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 1, now);

        dbContext.Add(campaign);
        await dbContext.SaveChangesAsync();

        TopUpRun run = TopUpRun.CreateManual(
            campaign,
            $"b003:{Guid.NewGuid():N}",
            requestedByUserId: 1,
            requestedAtUtc: now,
            note: "This note must not be exposed.");

        dbContext.Add(run);
        await dbContext.SaveChangesAsync();

        run.SetTotalSelected(selected);
        run.StartProcessing(now.AddSeconds(1));
        run.Finalize(
            totalProcessed: succeeded + failed + skipped,
            totalSucceeded: succeeded,
            totalFailed: failed,
            totalSkipped: skipped,
            totalAmount: totalCredited,
            utcNow: now.AddSeconds(2));
        await dbContext.SaveChangesAsync();

        return run.Id;
    }

    private sealed record RunSummaryEnvelope(RunSummaryData Data);

    private sealed record RunSummaryData(
        long RunId,
        long CampaignId,
        DateTime RunDateUtc,
        string TriggerType,
        string Status,
        int MatchedCount,
        int ProcessedCount,
        int SucceededCount,
        int FailedCount,
        int SkippedCount,
        decimal TotalCredited,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc);
}
