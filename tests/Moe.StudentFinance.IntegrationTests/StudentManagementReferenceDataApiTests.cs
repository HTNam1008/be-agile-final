using System.Net;
using System.Text.Json;
using System.Linq;
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
        var levelOptions = levels.EnumerateArray().ToArray();
        string[] levelValues = levelOptions
            .Select(option => GetProperty(option, "value").GetString() ?? string.Empty)
            .ToArray();
        Assert.Equal(["POST_SEC", "BACHELOR", "MASTER", "PHD"], levelValues);
        Assert.DoesNotContain(levelOptions, option =>
        {
            string? value = GetProperty(option, "value").GetString();
            return value is not null
                && (value.StartsWith("PRI_", StringComparison.Ordinal)
                    || value.StartsWith("SEC_", StringComparison.Ordinal)
                    || value.StartsWith("UNI_Y", StringComparison.Ordinal));
        });
        Assert.Contains(levelOptions, option =>
            GetProperty(option, "value").GetString() == "POST_SEC"
            && GetProperty(option, "label").GetString() == "Post-Secondary");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "BACHELOR"
            && GetProperty(option, "label").GetString() == "Bachelor");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "MASTER"
            && GetProperty(option, "label").GetString() == "Master");
        Assert.Contains(levels.EnumerateArray(), option =>
            GetProperty(option, "value").GetString() == "PHD"
            && GetProperty(option, "label").GetString() == "Doctor");

        JsonElement citizenshipStatuses = GetProperty(GetProperty(data, "studentListFilters"), "citizenshipStatuses");
        var citizenshipOptions = citizenshipStatuses.EnumerateArray().ToArray();
        Assert.Equal(3, citizenshipOptions.Length);
        Assert.Contains(citizenshipOptions, option =>
            GetProperty(option, "value").GetString() == "CITIZEN"
            && GetProperty(option, "label").GetString() == "Singapore Citizen");
        Assert.Contains(citizenshipOptions, option =>
            GetProperty(option, "value").GetString() == "PR"
            && GetProperty(option, "label").GetString() == "PR");
        Assert.Contains(citizenshipOptions, option =>
            GetProperty(option, "value").GetString() == "VALID_PASS_HOLDER"
            && GetProperty(option, "label").GetString() == "International Student");

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
