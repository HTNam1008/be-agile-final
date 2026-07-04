using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Application.Interest;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountInterestApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetInterestHistory_ReturnsOnlyAuthenticatedStudentsInterestCredits()
    {
        await SeedInterestCreditAsync();
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/api/eservice/v1/my-education-account/interest-history");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Headers.Add("X-Test-UserAccountId", "1003");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal(0.02m, data.GetProperty("annualInterestRate").GetDecimal());
        JsonElement item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal(2026, item.GetProperty("year").GetInt32());
        Assert.Equal(125.00m, item.GetProperty("openingBalance").GetDecimal());
        Assert.Equal(2.50m, item.GetProperty("interestAmount").GetDecimal());
        Assert.Equal(127.50m, item.GetProperty("closingBalance").GetDecimal());
    }

    [Fact]
    public async Task GetTransactions_WithInterestCategory_ReturnsInterestCredits()
    {
        await SeedInterestCreditAsync();
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/api/eservice/v1/my-education-account/transactions?category=INTEREST&page=1&pageSize=10");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Headers.Add("X-Test-UserAccountId", "1003");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        JsonElement item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.Equal("INTEREST", item.GetProperty("category").GetString());
        Assert.Equal(EducationAccountInterestCodes.TransactionTypeCode, item.GetProperty("transactionTypeCode").GetString());
    }

    private async Task SeedInterestCreditAsync()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == 2101);
        string idempotencyKey = EducationAccountInterestCodes.BuildIdempotencyKey(2026, account.Id);
        if (await db.Set<AccountTransaction>().AnyAsync(x => x.IdempotencyKey == idempotencyKey))
        {
            return;
        }

        AccountTransaction transaction = AccountTransaction.Create(
            account.Id,
            EducationAccountInterestCodes.TransactionTypeCode,
            2.50m,
            EducationAccountInterestCodes.ReferenceTypeCode,
            2026,
            idempotencyKey,
            125.00m,
            "Annual 2% interest for 2026",
            null,
            new DateTime(2027, 1, 1, 18, 30, 0, DateTimeKind.Utc));
        db.Set<AccountTransaction>().Add(transaction);
        account.UpdateBalance(2.50m);
        await db.SaveChangesAsync();
    }
}
