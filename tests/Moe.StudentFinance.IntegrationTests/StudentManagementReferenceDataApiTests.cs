using System.Net;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class StudentManagementReferenceDataApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Admin_Should_Get_StudentManagementReferenceData()
    {
        using HttpResponseMessage response = await _client.GetAsync("/api/admin/v1/reference-data/student-management");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = GetProperty(body.RootElement, "data");

        JsonElement levels = GetProperty(GetProperty(data, "studentProfile"), "levels");
        Assert.Contains(levels.EnumerateArray(), option => GetProperty(option, "value").GetString() == "PRI_1");
        Assert.Contains(levels.EnumerateArray(), option => GetProperty(option, "value").GetString() == "SEC_5");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "BACHELOR"
            && GetProperty(option, "label").GetString() == "Bachelor");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "MASTER"
            && GetProperty(option, "label").GetString() == "Master");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "PHD"
            && GetProperty(option, "label").GetString() == "PhD");
        Assert.DoesNotContain(levels.EnumerateArray(), option => GetProperty(option, "value").GetString() == "UNI_Y1");

        JsonElement openReasons = GetProperty(GetProperty(data, "educationAccount"), "openReasons");
        Assert.Equal(JsonValueKind.Array, openReasons.ValueKind);
        Assert.Empty(openReasons.EnumerateArray());

        JsonElement closeReasons = GetProperty(GetProperty(data, "educationAccount"), "closeReasons");
        Assert.Contains(closeReasons.EnumerateArray(), option => GetProperty(option, "value").GetString() == "STUDENT_INELIGIBLE");
    }

    [Fact]
    public async Task Anonymous_Should_Be_Denied()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/admin/v1/reference-data/student-management");
        request.Headers.Add("X-Test-Unauthenticated", "true");

        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new KeyNotFoundException(propertyName);
    }
}
