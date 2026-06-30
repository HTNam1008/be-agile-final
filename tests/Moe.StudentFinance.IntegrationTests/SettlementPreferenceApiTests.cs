using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class SettlementPreferenceApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSettlementPreference_WhenStudentHasNoAccount_ReturnsNotApplicable()
    {
        using HttpRequestMessage request = new(
            HttpMethod.Get,
            "/api/eservice/v1/my-education-account/settlement-preference");
        request.Headers.Add("X-Test-PersonId", "999901");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.False(data.GetProperty("isApplicable").GetBoolean());
        Assert.Equal(
            "Available once your Education Account is opened",
            data.GetProperty("emptyStateMessage").GetString());
    }

    [Fact]
    public async Task PutSettlementPreference_WhenCpfDestination_SavesActivePreference()
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            "/api/eservice/v1/my-education-account/settlement-preference")
        {
            Content = JsonContent.Create(new
            {
                destinationTypeCode = SettlementDestinationTypeCodes.Cpf,
                expectedUpdatedAtUtc = (DateTime?)null
            })
        };
        request.Headers.Add("X-Test-PersonId", "2101");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == 2101);
        SettlementPreference preference = await db.Set<SettlementPreference>()
            .SingleAsync(x => x.EducationAccountId == account.Id && x.IsActive);

        Assert.Equal(SettlementDestinationTypeCodes.Cpf, preference.DestinationTypeCode);
        Assert.Equal("CPF_DEFAULT", preference.DestinationToken);
        Assert.Equal("CPF account (linked to NRIC)", preference.DestinationMasked);
        Assert.True(preference.IsVerified);
    }
}
