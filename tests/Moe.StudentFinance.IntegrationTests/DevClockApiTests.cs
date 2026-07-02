using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class DevClockApiTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Clock_Endpoint_Should_Not_Map_When_Config_Disabled()
    {
        await using DisabledDevClockFactory disabledFactory = new();
        using HttpClient client = disabledFactory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/dev/clock");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Advance_Should_Move_Manual_Clock_And_Return_Utc_Date()
    {
        try
        {
            using HttpResponseMessage setClock = await _client.PutAsJsonAsync(
                "/dev/clock",
                new { utcNow = new DateTimeOffset(2026, 6, 30, 16, 0, 0, TimeSpan.Zero) });
            Assert.Equal(HttpStatusCode.OK, setClock.StatusCode);

            using HttpResponseMessage advance = await _client.PostAsJsonAsync(
                "/dev/clock/advance",
                new { days = 1, hours = 2 });
            Assert.Equal(HttpStatusCode.OK, advance.StatusCode);

            JsonElement body = await advance.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(
                new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
                body.GetProperty("utcNow").GetDateTimeOffset());
            Assert.Equal("2026-07-01", body.GetProperty("utcDate").GetString());
            Assert.True(body.GetProperty("isOverridden").GetBoolean());
        }
        finally
        {
            using HttpResponseMessage reset = await _client.DeleteAsync("/dev/clock");
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }
    }

    private sealed class DisabledDevClockFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DevTools:Clock:Enabled"] = "false"
                });
            });
        }
    }
}
