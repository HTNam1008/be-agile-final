using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotConversationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Sending_to_another_users_conversation_is_rejected()
    {
        // Person 2101 creates a conversation
        Guid conversationId = await CreateConversation(personId: 2101);

        // Person 2102 tries to use it -> must be rejected
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2102");
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message = "What is my balance?",
            pageContext = new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/account" }
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_conversation_returns_message_history_in_order()
    {
        Guid conversationId = await CreateConversation(personId: 2101);

        // Send a second message in the same conversation
        await SendChat("Show my outstanding bills", personId: 2101, conversationId: conversationId);

        // GET conversation history
        using HttpRequestMessage getRequest = new(HttpMethod.Get, $"/api/eservice/v1/ai/conversations/{conversationId}");
        getRequest.Headers.Add("X-Test-PersonId", "2101");
        using HttpResponseMessage getResponse = await _client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        JsonDocument doc = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.TryGetProperty("data", out JsonElement d) ? d : doc.RootElement;

        Assert.Equal(conversationId.ToString(), root.GetProperty("conversationId").GetString());
        JsonElement messages = root.GetProperty("messages");
        Assert.True(messages.GetArrayLength() >= 2, "Expected at least user + assistant message");

        // Messages must be in chronological order
        DateTime[] times = messages.EnumerateArray()
            .Select(m => m.GetProperty("createdAtUtc").GetDateTime())
            .ToArray();
        Assert.Equal(times, times.OrderBy(t => t).ToArray());
    }

    [Fact]
    public async Task Get_conversation_by_another_user_returns_not_found()
    {
        Guid conversationId = await CreateConversation(personId: 2101);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/eservice/v1/ai/conversations/{conversationId}");
        request.Headers.Add("X-Test-PersonId", "2102");
        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_nonexistent_conversation_returns_not_found()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/eservice/v1/ai/conversations/{Guid.NewGuid()}");
        request.Headers.Add("X-Test-PersonId", "2101");
        using HttpResponseMessage response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

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
        Assert.True(response.GetProperty("messageId").GetInt64() > 0);
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("text").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(response.GetProperty("mode").GetString()));
    }

    private async Task<Guid> CreateConversation(int personId)
    {
        JsonElement response = await SendChat("What is my balance?", personId: personId, conversationId: null);
        return response.GetProperty("conversationId").GetGuid();
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
