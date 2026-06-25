using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotContractTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly string[] KnownCardTypes = ["FINANCE_SUMMARY", "OUTSTANDING_BILLS", "PAYMENT_HISTORY", "FAS_RECOMMENDATION"];

    private static readonly string[] KnownActionTypes = ["NAVIGATE", "CONTACT_ADMIN_CENTER", "APPLY_FAS_PATCH"];

    [Fact]
    public async Task All_card_types_are_from_known_allowlist()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        foreach (JsonElement raw in response.GetProperty("cards").EnumerateArray())
        {
            string type = raw.GetProperty("type").GetString()!;
            Assert.Contains(type, KnownCardTypes);
        }
    }

    [Fact]
    public async Task All_action_types_are_from_known_allowlist()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        foreach (JsonElement raw in response.GetProperty("actions").EnumerateArray())
        {
            string type = raw.GetProperty("type").GetString()!;
            Assert.Contains(type, KnownActionTypes);
        }
    }

    [Fact]
    public async Task Navigate_actions_have_relative_routes_only()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        foreach (JsonElement action in response.GetProperty("actions").EnumerateArray())
        {
            if (action.GetProperty("type").GetString() != "NAVIGATE") continue;
            if (!action.TryGetProperty("route", out JsonElement route)) continue;
            if (route.ValueKind != JsonValueKind.String) continue;

            string routeValue = route.GetString()!;
            Assert.StartsWith("/", routeValue);
            Assert.DoesNotContain("..", routeValue);
            Assert.DoesNotContain("://", routeValue);
        }
    }

    [Fact]
    public async Task Response_has_expected_structure()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        Assert.True(response.GetProperty("conversationId").GetGuid() != Guid.Empty);
        Assert.True(response.GetProperty("messageId").GetInt64() > 0);
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("text").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("mode").GetString()));
        Assert.True(response.TryGetProperty("grounding", out _));
        Assert.True(response.TryGetProperty("cards", out _));
        Assert.True(response.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task Card_serialization_round_trip_preserves_types()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        string raw = response.GetRawText();
        JsonDocument reParsed = JsonDocument.Parse(raw);

        JsonElement cards = reParsed.RootElement.TryGetProperty("data", out JsonElement d) ? d : reParsed.RootElement;
        cards = cards.GetProperty("cards");

        HashSet<string> originalTypes = response.GetProperty("cards").EnumerateArray()
            .Select(x => x.GetProperty("type").GetString()!)
            .ToHashSet();

        Assert.NotEmpty(originalTypes);

        foreach (JsonElement card in cards.EnumerateArray())
        {
            string type = card.GetProperty("type").GetString()!;
            Assert.Contains(type, originalTypes);
        }
    }

    [Fact]
    public async Task Card_data_is_always_a_json_object()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        foreach (JsonElement card in response.GetProperty("cards").EnumerateArray())
        {
            string type = card.GetProperty("type").GetString()!;
            JsonElement data = card.GetProperty("data");

            if (type == "OUTSTANDING_BILLS" || type == "PAYMENT_HISTORY")
                Assert.Equal(JsonValueKind.Array, data.ValueKind);
            else
                Assert.Equal(JsonValueKind.Object, data.ValueKind);
        }
    }

    [Fact]
    public async Task Fas_interview_returns_expected_card_types()
    {
        JsonElement response = await Chat("I want to apply for FAS", personId: 2101);

        foreach (JsonElement raw in response.GetProperty("cards").EnumerateArray())
        {
            string type = raw.GetProperty("type").GetString()!;
            Assert.Contains(type, KnownCardTypes);
        }
    }

    [Fact]
    public async Task Page_context_is_allowlisted_before_persistence()
    {
        JsonElement response = await ChatWithContext("What can I pay from my education account?", 2101, new
        {
            domain = "PAYMENT<script>",
            surface = new string('x', 120),
            path = "https://evil.example/path",
            entity = new { nric = "S1234567A", token = "secret" }
        });
        Guid conversationId = response.GetProperty("conversationId").GetGuid();

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        AiConversation stored = db.Set<AiConversation>().Single(x => x.Id == conversationId);

        JsonDocument json = JsonDocument.Parse(stored.PageContextJson!);
        JsonElement root = json.RootElement;
        Assert.Equal("GENERAL", root.GetProperty("domain").GetString());
        Assert.Equal(80, root.GetProperty("surface").GetString()!.Length);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("path").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("entity").ValueKind);
    }

    private async Task<JsonElement> Chat(string message, int personId, Guid? conversationId = null)
        => await ChatWithContext(message, personId, new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/account" }, conversationId);

    private async Task<JsonElement> ChatWithContext(string message, int personId, object pageContext, Guid? conversationId = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", personId.ToString());
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message,
            pageContext
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Expected 200 OK, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        return root.TryGetProperty("data", out JsonElement data) ? data : root;
    }
}
