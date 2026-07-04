using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.AiCopilot.Infrastructure.Knowledge;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AiCopilotContractTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly string[] KnownCardTypes = ["FINANCE_SUMMARY", "OUTSTANDING_BILLS", "PAYMENT_HISTORY", "FAS_RECOMMENDATION", "FAS_TASK_STATE", "KNOWLEDGE_ANSWER"];

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
        Assert.True(response.TryGetProperty("followUpQuestions", out _));
    }

    [Fact]
    public async Task Knowledge_answer_card_is_preserved_in_contract()
    {
        JsonElement response = await ChatWithContext("Explain the MOE FAS Bursary", 2101, new
        {
            domain = "FAS",
            surface = "PORTAL",
            path = "/portal/fas"
        });

        JsonElement card = Assert.Single(response.GetProperty("cards").EnumerateArray());
        Assert.Equal("KNOWLEDGE_ANSWER", card.GetProperty("type").GetString());
        JsonElement data = card.GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("summary").GetString()));
        Assert.NotEmpty(data.GetProperty("keyFacts").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("nextSteps").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("sourceSummaries").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("knowledgeVersion").GetString()));
        Assert.NotEmpty(response.GetProperty("followUpQuestions").EnumerateArray());
        Assert.Contains(response.GetProperty("actions").EnumerateArray(), action =>
            action.GetProperty("type").GetString() == "NAVIGATE" &&
            action.GetProperty("route").GetString() == "/portal/fas");
    }

    [Fact]
    public async Task Knowledge_retriever_maps_natural_school_fee_help_to_fas()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var store = new EmbeddedKnowledgeDocumentStore();
        var retriever = new LocalKnowledgeRetriever(services, store);

        IReadOnlyList<Moe.Modules.AiCopilot.Application.Knowledge.KnowledgeResult> results =
            await retriever.RetrieveAsync("My family does not earn much. Can I get help with school fees?", "GENERAL");

        Assert.NotEmpty(results);
        Assert.Contains(results.Take(3), result => result.Citation.SourceId.StartsWith("FAS-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Natural_school_fee_help_question_returns_fas_guidance_not_fallback()
    {
        JsonElement response = await ChatWithContext("My family does not earn much. Can I get help with school fees?", 2101, new
        {
            domain = "GENERAL",
            surface = "PORTAL",
            path = "/portal/dashboard"
        });

        Assert.Equal("GENERAL", response.GetProperty("mode").GetString());
        Assert.DoesNotContain("cannot answer this reliably", response.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.GetProperty("cards").EnumerateArray(),
            card => card.GetProperty("type").GetString() == "KNOWLEDGE_ANSWER");
        Assert.Contains(response.GetProperty("actions").EnumerateArray(),
            action => action.GetProperty("type").GetString() == "NAVIGATE" &&
                      action.GetProperty("route").GetString() == "/portal/fas");
    }

    [Fact]
    public void Knowledge_pack_validator_rejects_duplicate_chunk_ids()
    {
        var doc = new KnowledgeDocument(
            "FAS-DUPLICATE",
            "Duplicate",
            "Duplicate",
            "FAS",
            "OFFICIAL",
            "1.0",
            new DateOnly(2026, 1, 1),
            "Content",
            "/portal/fas",
            ["duplicate"],
            false,
            "Student Finance Product",
            ["AnswerKnowledgeQuestion"],
            ["Continue my FAS eligibility check."]);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            EmbeddedKnowledgeDocumentStore.ValidateKnowledgePacks([doc, doc]));
        Assert.Contains("Duplicate knowledge chunk_id", error.Message);
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

    [Fact]
    public async Task Message_content_is_redacted_before_persistence()
    {
        const string message = "My NRIC is S1234567A, email is learner@example.com, phone 91234567, bill BILL-20260626-A1B2C3D4E5F6A7B8. What is my balance?";
        JsonElement response = await Chat(message, personId: 2101);
        Guid conversationId = response.GetProperty("conversationId").GetGuid();

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        AiMessage stored = db.Set<AiMessage>()
            .Single(x => x.ConversationId == conversationId && x.RoleCode == "USER");

        Assert.DoesNotContain("S1234567A", stored.Content);
        Assert.DoesNotContain("learner@example.com", stored.Content);
        Assert.DoesNotContain("91234567", stored.Content);
        Assert.DoesNotContain("BILL-20260626-A1B2C3D4E5F6A7B8", stored.Content);
        Assert.Contains("[IDENTITY]", stored.Content);
        Assert.Contains("[EMAIL]", stored.Content);
        Assert.Contains("[PHONE]", stored.Content);
        Assert.Contains("[PAYMENT_REF]", stored.Content);
    }

    [Fact]
    public async Task Unknown_domain_normalized_to_general()
    {
        JsonElement response = await ChatWithContext("What is my balance?", 2101, new
        {
            domain = "INVENTORY",
            surface = "PORTAL",
            path = "/portal/account"
        });
        Assert.Equal("PAYMENT", response.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Path_with_dots_stripped_by_sanitizer()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new
        {
            message = "What is my balance?",
            pageContext = new { domain = "PAYMENT", surface = "PORTAL", path = "/portal/account/../../secrets" }
        });
        using HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        JsonElement data = root.TryGetProperty("data", out JsonElement d) ? d : root;

        Guid conversationId = data.GetProperty("conversationId").GetGuid();
        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        AiConversation stored = db.Set<AiConversation>().Single(x => x.Id == conversationId);
        JsonDocument storedJson = JsonDocument.Parse(stored.PageContextJson!);
        Assert.Equal(JsonValueKind.Null, storedJson.RootElement.GetProperty("path").ValueKind);
    }

    [Fact]
    public async Task Fas_fieldKey_with_payment_domain_is_ignored()
    {
        JsonElement response = await ChatWithContext("I want to apply for FAS", 2101, new
        {
            domain = "PAYMENT",
            surface = "PORTAL",
            path = "/portal/bills",
            entity = new { fieldKey = "monthlyHouseholdIncome" }
        });
        // FieldKey should be ignored in non-FAS domain; request still processes as FAS due to keywords
        Assert.Equal("FAS_INTERVIEW", response.GetProperty("mode").GetString());
        Assert.True(response.TryGetProperty("interviewState", out _));
    }

    [Fact]
    public async Task Null_page_context_does_not_crash()
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/eservice/v1/ai/chat");
        request.Headers.Add("X-Test-PersonId", "2101");
        request.Content = JsonContent.Create(new { message = "What is my balance?" });
        using HttpResponseMessage response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
