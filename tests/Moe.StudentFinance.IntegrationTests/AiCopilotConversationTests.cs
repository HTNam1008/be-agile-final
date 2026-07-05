using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotConversationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Unauthenticated_chat_request_is_rejected()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-Unauthenticated", "true");
        request.Content = JsonContent.Create(new { message = "What is my balance?" });

        using HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Chat_response_always_includes_conversation_id_and_message_id()
    {
        JsonElement response = await SendChat("What is my balance?", personId: 2101, conversationId: null);

        Assert.True(response.GetProperty("conversationId").GetGuid() != Guid.Empty);
        Assert.True(response.GetProperty("messageId").GetInt64() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("text").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("mode").GetString()));
    }

    private async Task<JsonElement> SendChat(string message, int personId, Guid? conversationId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", personId.ToString());
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message,
            pageContext = new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/account" }
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Expected 200 OK, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        return root.TryGetProperty("data", out JsonElement data) ? data : root;
    }
}
