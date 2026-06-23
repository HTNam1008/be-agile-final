using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountOpenApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenManualEndpoint_Should_Require_InternalProvisioningPermission()
    {
        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/education-accounts",
            new
            {
                personId = 2101,
                reasonCode = "MANUAL_CREATE",
                remarks = "Should be internal only"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
