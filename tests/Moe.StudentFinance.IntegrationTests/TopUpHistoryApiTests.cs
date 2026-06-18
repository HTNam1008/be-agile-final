using System.Net;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class TopUpHistoryApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TopUpHistoryApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Campaign_History_Endpoint_Should_Return_Paged_Response()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/admin/v1/top-up-history/campaigns?page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Run_History_Endpoint_Should_Deny_Organization_Outside_Scope()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/admin/v1/top-up-history/runs?organizationId=999&page=1&pageSize=25");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task History_Endpoint_Should_Reject_Unbounded_Page_Size()
    {
        HttpResponseMessage response = await _client.GetAsync(
            "/api/admin/v1/top-up-history/runs?page=1&pageSize=1000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
