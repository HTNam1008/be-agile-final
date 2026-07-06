using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotIntentRoutingTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Dictionary<Guid, JsonElement> _fasStates = [];

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
    public async Task Bursary_question_prioritizes_bursary_source()
    {
        JsonElement response = await Chat("Explain the MOE FAS Bursary", personId: 2101);

        JsonElement[] citations = response.GetProperty("grounding").GetProperty("citations").EnumerateArray().ToArray();
        bool hasBursarySource = citations.Any(c =>
            c.GetProperty("sourceId").GetString()!.Contains("BURSARY", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasBursarySource, "Expected at least one citation from a bursary source");
        Assert.True(response.GetProperty("grounding").GetProperty("isGrounded").GetBoolean());
    }

    [Fact]
    public async Task Fas_process_question_prioritizes_application_process_source()
    {
        JsonElement response = await Chat("Walk me through the FAS application process.", personId: 2101);

        JsonElement firstCitation = response.GetProperty("grounding").GetProperty("citations").EnumerateArray().First();
        Assert.Contains("APPLICATION", firstCitation.GetProperty("sourceId").GetString());
        Assert.Contains("application", firstCitation.GetProperty("section").GetString()!.ToLowerInvariant());
    }

    [Fact]
    public async Task Income_document_question_prioritizes_supporting_document_guidance()
    {
        JsonElement response = await Chat("What documents prove income?", personId: 2101);

        JsonElement firstCitation = response.GetProperty("grounding").GetProperty("citations").EnumerateArray().First();
        Assert.Equal("FAS-APPLICATION-001", firstCitation.GetProperty("sourceId").GetString());
        string cardJson = response.GetProperty("cards").EnumerateArray().Single().GetRawText();
        Assert.Contains("Income proof", cardJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fas_definition_question_does_not_start_interview()
    {
        JsonElement response = await Chat("What is FAS?", personId: 2101);
        Assert.Contains(response.GetProperty("mode").GetString(), new[] { "GENERAL", "FALLBACK" });
        Assert.False(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Live_scheme_eligibility_question_starts_interview()
    {
        JsonElement response = await Chat("Which schemes can I apply for?", personId: 2101);

        Assert.Equal("FAS_INTERVIEW", response.GetProperty("mode").GetString());
        Assert.True(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Eligibility_without_fas_context_routes_to_general()
    {
        JsonElement response = await Chat("Am I eligible?", personId: 2101);
        Assert.Contains(response.GetProperty("mode").GetString(), new[] { "GENERAL", "FALLBACK" });
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
        foreach (JsonElement card in response.GetProperty("cards").EnumerateArray())
        {
            string type = card.GetProperty("type").GetString()!;
            Assert.DoesNotContain(type, new[] { "FINANCE_SUMMARY", "FAS_RECOMMENDATION" });
        }
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
    public async Task Fas_knowledge_question_can_interrupt_interview_and_then_resume()
    {
        JsonElement first = await Chat("I want to apply for FAS", personId: 2101);
        Guid cid = first.GetProperty("conversationId").GetGuid();
        Assert.Equal("FAS_INTERVIEW", first.GetProperty("mode").GetString());

        JsonElement pci = await Chat("How is PCI calculated?", personId: 2101, conversationId: cid);
        Assert.Equal("GENERAL", pci.GetProperty("mode").GetString());
        Assert.True(pci.GetProperty("grounding").GetProperty("isGrounded").GetBoolean());
        Assert.False(pci.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
        JsonElement resumed = await Chat("Continue my FAS eligibility check.", personId: 2101, conversationId: cid);
        Assert.Equal("FAS_INTERVIEW", resumed.GetProperty("mode").GetString());
        Assert.Equal("isWelfareHomeResident", resumed.GetProperty("interviewState").GetProperty("missingFields")[0].GetString());
    }

    [Fact]
    public async Task Fas_knowledge_question_without_interview_does_not_suggest_resume()
    {
        JsonElement response = await Chat("How is PCI calculated?", personId: 2101);

        Assert.Equal("GENERAL", response.GetProperty("mode").GetString());
        Assert.StartsWith("PCI means per-capita income", response.GetProperty("text").GetString());
        Assert.DoesNotContain(response.GetProperty("followUpQuestions").EnumerateArray(),
            x => x.GetString() == "Continue my FAS eligibility check.");
        Assert.DoesNotContain(response.GetProperty("followUpQuestions").EnumerateArray(),
            x => x.GetString() == "How is PCI calculated?");
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
    public async Task Feel_like_doing_fas_starts_interview()
    {
        JsonElement response = await Chat("I feel like doing fas", personId: 2101);

        Assert.Equal("FAS_INTERVIEW", response.GetProperty("mode").GetString());
        Assert.Equal("START_FAS", response.GetProperty("turnIntent").GetString());
        Assert.True(response.TryGetProperty("interviewState", out JsonElement interviewState) && interviewState.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Likely_fas_typo_clarifies_instead_of_generic_fallback()
    {
        JsonElement response = await Chat("i feel like doing fss", personId: 2101);

        Assert.Equal("GENERAL", response.GetProperty("mode").GetString());
        Assert.Equal("CLARIFY_FAS_TYPO", response.GetProperty("turnIntent").GetString());
        Assert.Contains("Did you mean FAS", response.GetProperty("text").GetString());
        Assert.DoesNotContain("cannot answer this reliably", response.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Education_account_usage_routes_to_payment_tools()
    {
        JsonElement response = await Chat("What can I use my Education Account for?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        Assert.Contains(response.GetProperty("cards").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
        Assert.DoesNotContain("cannot answer this reliably", response.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fas_on_payment_page_does_not_get_contaminated()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new
        {
            message = "help me with fas",
            pageContext = new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/payments" }
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
        JsonElement? fasState = conversationId.HasValue && _fasStates.TryGetValue(conversationId.Value, out JsonElement state) ? state : null;
        request.Content = JsonContent.Create(new
        {
            conversationId,
            message,
            pageContext = new { domain = "PORTAL", surface = "PORTAL", path = "/portal" },
            fasState
        });

        using HttpResponseMessage response = await _client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
            Assert.Fail($"Expected 200 OK, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        JsonElement data = root.TryGetProperty("data", out JsonElement d) ? d : root;
        RememberFasState(data);
        return data;
    }

    private void RememberFasState(JsonElement response)
    {
        if (!response.TryGetProperty("conversationId", out JsonElement cidElement) || cidElement.ValueKind != JsonValueKind.String) return;
        if (!response.TryGetProperty("fasState", out JsonElement state) || state.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;
        _fasStates[cidElement.GetGuid()] = state.Clone();
    }
}
