using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotIntentRoutingTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Payment_keyword_routes_to_payment_mode()
    {
        JsonElement response = await Chat("What is my balance?", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Fas_keyword_routes_to_fas_interview_mode()
    {
        JsonElement response = await Chat("I want to apply for FAS", personId: 2101);
        Assert.Equal("FAS_INTERVIEW", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Financial_assistance_definition_stays_general()
    {
        JsonElement response = await Chat("Tell me about financial assistance", personId: 2101);
        Assert.Equal("GENERAL", response.GetProperty("mode").GetString());
        Assert.False(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Explain the MOE FAS Bursary")]
    [InlineData("What is the Tiered Fee Subsidy scheme?")]
    [InlineData("How does the JC/CI FAS scheme work?")]
    public async Task Fas_scheme_information_routes_to_knowledge_base(string message)
    {
        JsonElement response = await Chat(message, personId: 2101);
        Assert.Equal("GENERAL", response.GetProperty("mode").GetString());
        Assert.True(response.GetProperty("grounding").GetProperty("isGrounded").GetBoolean());
        Assert.NotEmpty(response.GetProperty("grounding").GetProperty("citations").EnumerateArray());
        Assert.False(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Fas_definition_question_does_not_start_interview()
    {
        JsonElement response = await Chat("What is FAS?", personId: 2101);
        Assert.Contains(response.GetProperty("mode").GetString(), new[] { "GENERAL", "FALLBACK" });
        Assert.False(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Eligibility_keyword_routes_to_fas()
    {
        JsonElement response = await Chat("Am I eligible?", personId: 2101);
        Assert.Equal("FAS_INTERVIEW", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Bill_keyword_routes_to_payment()
    {
        JsonElement response = await Chat("Show my bills", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Refund_keyword_routes_to_payment()
    {
        JsonElement response = await Chat("Have I received any refunds?", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task General_query_does_not_route_to_finance_or_fas_tools()
    {
        JsonElement response = await Chat("What is MOE?", personId: 2101);
        string? mode = response.GetProperty("mode").GetString();

        Assert.Contains(mode, new[] { "GENERAL", "FALLBACK" });
        Assert.Empty(response.GetProperty("cards").EnumerateArray());
        Assert.False(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Fas_interview_persists_across_turns()
    {
        Guid? cid = null;

        JsonElement first = await Chat("I want to apply for FAS", personId: 2101, conversationId: cid);
        Assert.Equal("FAS_INTERVIEW", first.GetProperty("mode").GetString());
        cid = first.GetProperty("conversationId").GetGuid();

        JsonElement second = await Chat("No", personId: 2101, conversationId: cid);
        Assert.Equal("FAS_INTERVIEW", second.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Outstanding_keyword_routes_to_payment()
    {
        JsonElement response = await Chat("What is my outstanding amount?", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Pay_keyword_routes_to_payment()
    {
        JsonElement response = await Chat("How do I pay my course fees?", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Theory]
    [InlineData("i want to do fas, help me")]
    [InlineData("help me with fas")]
    [InlineData("i want to apply for financial assistance")]
    [InlineData("can you guide me through fas")]
    [InlineData("how do i do fas")]
    [InlineData("i have a question about fas")]
    public async Task Natural_fas_phrasing_routes_to_fas_interview(string message)
    {
        JsonElement response = await Chat(message, personId: 2101);
        string? mode = response.GetProperty("mode").GetString();
        Assert.True(mode == "FAS_INTERVIEW",
            $"Expected FAS_INTERVIEW for \"{message}\", got {mode}");
    }

    [Fact]
    public async Task Fas_on_payment_page_does_not_get_contaminated()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new
        {
            message = "help me with fas",
            pageContext = new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/bills" }
        });
        using HttpResponseMessage response = await _client.SendAsync(request);
        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        JsonElement data = root.TryGetProperty("data", out JsonElement d) ? d : root;
        string? mode = data.GetProperty("mode").GetString();
        Assert.Equal("FAS_INTERVIEW", mode);
    }

    private async Task<JsonElement> Chat(string message, int personId, Guid? conversationId = null)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", personId.ToString());
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message,
            pageContext = new { domain = "PORTAL", surface = "PORTAL", path = "/portal" }
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Expected 200 OK, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        return root.TryGetProperty("data", out JsonElement data) ? data : root;
    }
}
