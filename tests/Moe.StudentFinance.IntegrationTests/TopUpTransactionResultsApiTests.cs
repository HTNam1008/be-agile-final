using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class TopUpTransactionResultsApiTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TopUpTransactionResultsApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Results_Should_Be_Paged_Filtered_And_Masked()
    {
        SeededRun seeded = await SeedRunWithTransactionsAsync(organizationId: 1);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/admin/v1/top-up/runs/{seeded.RunId}/transactions"
            + "?status=failed"
            + $"&studentOrAccountSearch={Uri.EscapeDataString(seeded.StudentName)}"
            + "&reason=rejected&page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        TransactionResultsEnvelope? payload =
            await response.Content.ReadFromJsonAsync<TransactionResultsEnvelope>();

        Assert.NotNull(payload);
        Assert.Equal(1, payload.Data.TotalCount);
        TransactionResultData item = Assert.Single(payload.Data.Items);
        Assert.Equal(TopUpTransactionStatusCodes.Failed, item.Status);
        Assert.Equal("EA-****-9876", item.MaskedAccountNumber);
        Assert.NotNull(item.MaskedStudentNumber);
        Assert.EndsWith("4321", item.MaskedStudentNumber, StringComparison.Ordinal);
        Assert.StartsWith("****", item.MaskedStudentNumber, StringComparison.Ordinal);
        Assert.Equal(seeded.StudentName, item.StudentDisplayName);
        Assert.Equal(SafeReasons.CreditRejected, item.Reason);

        string json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(seeded.RawAccountNumber, json, StringComparison.Ordinal);
        Assert.DoesNotContain(seeded.RawStudentNumber, json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"idempotencyKey\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"personId\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"educationAccountId\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Results_Should_Deny_Run_Outside_Organization_Scope()
    {
        SeededRun seeded = await SeedRunWithTransactionsAsync(organizationId: 999);

        HttpResponseMessage response = await _client.GetAsync(
            $"/api/admin/v1/top-up/runs/{seeded.RunId}/transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Results_Should_Return_NotFound_For_Unknown_Run()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/admin/v1/top-up/runs/9223372036854770000/transactions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Results_Should_Reject_Unbounded_Page_Size()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/admin/v1/top-up/runs/1/transactions?pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<SeededRun> SeedRunWithTransactionsAsync(long organizationId)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        MoeDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        long personId = Random.Shared.NextInt64(100_000, 900_000);
        string studentName = $"B004 Student {suffix}";
        string rawStudentNumber = $"B004-STU-{suffix}-4321";
        string rawAccountNumber = $"EA-{suffix}-9876";
        DateTime now = DateTime.UtcNow;

        Person person = new(
            personId,
            $"b004-person-{suffix}",
            studentName,
            new DateOnly(2012, 1, 1),
            "SG",
            "CITIZEN");
        SchoolEnrollment enrollment = CreateEnrollment(
            personId,
            organizationId,
            rawStudentNumber,
            now);
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            rawAccountNumber,
            new DateTimeOffset(now, TimeSpan.Zero),
            "B004_TEST",
            "B-004 integration test",
            openedBy: 1).Value;

        dbContext.AddRange(person, enrollment, account);
        await dbContext.SaveChangesAsync();

        TopUpCampaign campaign = TopUpCampaign.Create(
            organizationId,
            $"B004-{suffix}",
            "B-004 integration campaign",
            null,
            "FIXED_SELECTION",
            100m,
            "B-004 integration test",
            "IMMEDIATE",
            DateOnly.FromDateTime(now),
            null,
            null,
            null,
            deliveryTypeCode: "INSTANT",
            maxTotalAmount: 100m,
            currentUserId: 1,
            now);
        campaign.ChangeStatus(TopUpCampaignStatusCodes.Active, 1, now);
        dbContext.Add(campaign);
        await dbContext.SaveChangesAsync();

        TopUpRun run = TopUpRun.CreateManual(
            campaign,
            $"b004:{suffix}",
            requestedByUserId: 1,
            requestedAtUtc: now,
            note: null);
        dbContext.Add(run);
        await dbContext.SaveChangesAsync();

        TopUpTransaction failed = TopUpTransaction.Create(
            run.Id,
            account.Id,
            100m,
            now.AddMinutes(1));
        failed.Fail(SafeReasons.CreditRejected, now.AddMinutes(2));

        dbContext.Add(failed);
        await dbContext.SaveChangesAsync();

        return new SeededRun(
            run.Id,
            studentName,
            rawStudentNumber,
            rawAccountNumber);
    }

    private static SchoolEnrollment CreateEnrollment(
        long personId,
        long organizationId,
        string studentNumber,
        DateTime now)
    {
        SchoolEnrollment enrollment =
            (SchoolEnrollment)Activator.CreateInstance(
                typeof(SchoolEnrollment),
                nonPublic: true)!;

        Set(enrollment, nameof(SchoolEnrollment.PersonId), personId);
        Set(enrollment, nameof(SchoolEnrollment.OrganizationId), organizationId);
        Set(enrollment, nameof(SchoolEnrollment.StudentNumber), studentNumber);
        Set(enrollment, nameof(SchoolEnrollment.AcademicYear), "2026");
        Set(enrollment, nameof(SchoolEnrollment.LevelCode), "SEC_2");
        Set(enrollment, nameof(SchoolEnrollment.ClassCode), "2A");
        Set(enrollment, nameof(SchoolEnrollment.SchoolingStatusCode), "ACTIVE");
        Set(enrollment, nameof(SchoolEnrollment.StartDate), new DateOnly(2026, 1, 1));
        Set(enrollment, nameof(SchoolEnrollment.SourceCode), "B004_TEST");
        Set(enrollment, nameof(SchoolEnrollment.CreatedAtUtc), now);
        Set(enrollment, nameof(SchoolEnrollment.UpdatedAtUtc), now);
        return enrollment;
    }

    private static void Set<T>(SchoolEnrollment enrollment, string property, T value)
        => typeof(SchoolEnrollment).GetProperty(property)!.SetValue(enrollment, value);

    private sealed record SeededRun(
        long RunId,
        string StudentName,
        string RawStudentNumber,
        string RawAccountNumber);

    private sealed record TransactionResultsEnvelope(TransactionResultsPageData Data);

    private sealed record TransactionResultsPageData(
        IReadOnlyList<TransactionResultData> Items,
        int Page,
        int PageSize,
        long TotalCount);

    private sealed record TransactionResultData(
        long TransactionId,
        string MaskedAccountNumber,
        string? MaskedStudentNumber,
        string StudentDisplayName,
        decimal Amount,
        string CurrencyCode,
        string Status,
        string? Reason,
        long? AccountTransactionId,
        DateTime CreatedAtUtc,
        DateTime? CompletedAtUtc);
}
