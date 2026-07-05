using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotPaymentTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Balance_question_returns_finance_summary_card()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        JsonElement card = response.GetProperty("cards").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
        JsonElement data = card.GetProperty("data");
        Assert.True(data.TryGetProperty("availableBalance", out _));
        Assert.True(data.TryGetProperty("totalOutstanding", out _));
        Assert.True(data.TryGetProperty("netAvailable", out _));
        string text = response.GetProperty("text").GetString()!;
        Assert.True(
            text.Contains("available", StringComparison.OrdinalIgnoreCase),
            $"Expected text to contain 'available', but got: '{text}'");
    }

    [Fact]
    public async Task Balance_response_includes_navigate_actions()
    {
        JsonElement response = await Chat("What is my education account balance?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        Assert.Contains(response.GetProperty("actions").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "NAVIGATE");
    }

    [Fact]
    public async Task Outstanding_bills_question_returns_bills_card()
    {
        JsonElement response = await Chat("Show my outstanding bills", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        JsonElement card = response.GetProperty("cards").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "OUTSTANDING_BILLS");
        // Card data must be an array (even if empty for this seeded user)
        Assert.Equal(JsonValueKind.Array, card.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Outstanding_bills_text_mentions_bill_count_or_zero()
    {
        JsonElement response = await Chat("What bills are outstanding?", personId: 2101);

        string text = response.GetProperty("text").GetString()!;
        // Either "no outstanding bills" or "X outstanding bill(s)"
        Assert.True(
            text.Contains("outstanding", StringComparison.OrdinalIgnoreCase),
            $"Expected text to mention outstanding status, got: {text}");
    }

    [Fact]
    public async Task Payment_history_question_returns_history_card()
    {
        JsonElement response = await Chat("Show my recent payment history", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        JsonElement card = response.GetProperty("cards").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "PAYMENT_HISTORY");
        Assert.Equal(JsonValueKind.Array, card.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Payment_follow_ups_do_not_repeat_current_question()
    {
        JsonElement response = await Chat("Show my recent payment history and refunds.", personId: 2101);

        Assert.DoesNotContain(response.GetProperty("followUpQuestions").EnumerateArray(),
            x => x.GetString() == "Show my recent payment history and refunds.");
    }

    [Fact]
    public async Task Refund_keyword_routes_to_payment_history()
    {
        JsonElement response = await Chat("Have I received any refunds?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        Assert.Contains(response.GetProperty("cards").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "PAYMENT_HISTORY");
    }

    [Fact]
    public async Task Second_payment_message_in_same_conversation_preserves_payment_mode()
    {
        JsonElement first = await Chat("What is my balance?", personId: 2101);
        Guid conversationId = first.GetProperty("conversationId").GetGuid();

        JsonElement second = await Chat("Show my outstanding bills", personId: 2101, conversationId: conversationId);

        Assert.Equal("PAYMENT", second.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Finance_snapshot_currency_is_SGD()
    {
        JsonElement response = await Chat("What is my education account balance?", personId: 2101);

        JsonElement card = response.GetProperty("cards").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
        string currency = card.GetProperty("data").GetProperty("currencyCode").GetString()!;
        Assert.Equal("SGD", currency, ignoreCase: true);
    }

    [Fact]
    public async Task General_payment_query_catches_all_and_returns_finance_summary()
    {
        JsonElement response = await Chat("How much money do I have for school?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        Assert.Contains(response.GetProperty("cards").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
        string text = response.GetProperty("text").GetString()!;
        Assert.Contains("available", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Withdraw_query_returns_payment_mode_with_navigate_actions()
    {
        JsonElement response = await Chat("I want to withdraw from a course", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        JsonElement[] actions = response.GetProperty("actions").EnumerateArray().ToArray();
        Assert.Contains(actions, x => x.GetProperty("type").GetString() == "NAVIGATE");
    }

    [Fact]
    public async Task General_payment_query_mentions_no_outstanding_charges_when_none_seeded()
    {
        JsonElement response = await Chat("What is my account summary?", personId: 2101);

        string text = response.GetProperty("text").GetString()!;
        Assert.Contains("nothing is due", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Payment_mode_persists_across_turns_with_varied_keywords()
    {
        JsonElement first = await Chat("What is my balance?", personId: 2101);
        Guid conversationId = first.GetProperty("conversationId").GetGuid();

        JsonElement second = await Chat("Show my bills", personId: 2101, conversationId: conversationId);
        Assert.Equal("PAYMENT", second.GetProperty("mode").GetString());

        JsonElement third = await Chat("What about refunds?", personId: 2101, conversationId: conversationId);
        Assert.Equal("PAYMENT", third.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Payment_with_fas_keyword_in_context_maintains_correct_mode()
    {
        JsonElement response = await Chat("What can I pay for?", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        Assert.Contains(response.GetProperty("cards").EnumerateArray(),
            x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
    }

    [Fact]
    public async Task Payment_with_irrelevant_text_still_routes_to_payment()
    {
        JsonElement response = await Chat("Hello there", personId: 2101);
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        string text = response.GetProperty("text").GetString()!;
        Assert.Contains("available", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Finance_snapshot_reflects_account_balance_correctly()
    {
        JsonElement response = await Chat("What can I pay from my education account?", personId: 2101);

        JsonElement card = response.GetProperty("cards").EnumerateArray()
            .Single(x => x.GetProperty("type").GetString() == "FINANCE_SUMMARY");
        JsonElement data = card.GetProperty("data");
        Assert.True(data.GetProperty("availableBalance").GetDecimal() >= 0);
        Assert.Equal("SGD", data.GetProperty("currencyCode").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Payment_response_always_includes_grounding_citations()
    {
        JsonElement response = await Chat("What is my education account balance?", personId: 2101);

        Assert.True(response.GetProperty("grounding").GetProperty("isGrounded").GetBoolean());
        Assert.NotEmpty(response.GetProperty("grounding").GetProperty("citations").EnumerateArray());
    }

    [Fact]
    public async Task Payment_response_is_always_grounded()
    {
        JsonElement response = await Chat("Can I request a top-up myself?", personId: 2101);

        Assert.True(response.GetProperty("grounding").GetProperty("isGrounded").GetBoolean());
        Assert.NotEmpty(response.GetProperty("grounding").GetProperty("citations").EnumerateArray());
    }

    [Fact]
    public async Task How_to_pay_with_no_outstanding_bills_answers_without_fallback()
    {
        JsonElement response = await Chat("How do I pay this bill?", personId: 2101);

        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
        string text = response.GetProperty("text").GetString()!;
        Assert.Contains("do not have an outstanding bill", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("When a bill is due", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cannot answer this reliably", text, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<JsonElement> Chat(string message, int personId, Guid? conversationId = null)
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
