using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiStreamingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _kernel;
    private readonly IKnowledgeRetriever _knowledge;
    private readonly ILogger<AiStreamingService> _logger;
    private readonly MoeDbContext _db;
    private readonly SensitiveDataRedactor _redactor;

    public AiStreamingService(Kernel kernel, IKnowledgeRetriever knowledge, ILogger<AiStreamingService> logger,
        MoeDbContext db, SensitiveDataRedactor redactor)
    {
        _kernel = kernel;
        _knowledge = knowledge;
        _logger = logger;
        _db = db;
        _redactor = redactor;
    }

    public async Task StreamResponseAsync(HttpContext httpContext, AiConversation conversation, AiChatRequest request, CancellationToken ct)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";

        var sw = Stopwatch.StartNew();

        try
        {
            IReadOnlyList<KnowledgeResult> knowledgeResults = await _knowledge.RetrieveAsync(request.Message, request.PageContext?.Domain, ct: ct);

            string sourceText = knowledgeResults.Count > 0
                ? string.Join("\n\n", knowledgeResults.Select(r => $"[{r.Citation.SourceId}] {r.Content}"))
                : "No relevant policy documents found.";

            var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory(
                "You are the MOE Student Finance Copilot for Singapore. Answer like a calm counter officer, not a policy document.\n" +
                "Keep the answer under 120 words. Lead with the direct answer. Ask at most one next question.\n" +
                "Use no more than three bullets. Do not include source IDs, bracket citations, or raw document codes in the answer text; the UI renders sources separately.\n" +
                "Never invent personal data, policy, eligibility, amounts, status, or timelines.\n" +
                "Label prototype uncertainty in plain language only when it affects the answer.\n" +
                $"Sources:\n{sourceText}");
            history.AddUserMessage(request.Message);

            var fullText = new System.Text.StringBuilder();
            Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService chat = _kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            await foreach (StreamingChatMessageContent chunk in chat.GetStreamingChatMessageContentsAsync(history, kernel: _kernel, cancellationToken: ct))
            {
                string? token = chunk.Content;
                if (string.IsNullOrEmpty(token)) continue;
                fullText.Append(token);
                await WriteSseEventAsync(httpContext, "text", token, ct);
            }

            string finalText = fullText.ToString();
            string mode = conversation.FasSession?.StatusCode is "COLLECTING" or "CONFIRMING" or "CLARIFYING"
                ? "FAS_INTERVIEW"
                : AiKeywordMatchers.DetermineMode(request.Message, conversation.ModeCode, request.PageContext?.Domain);

            AiInterviewState? interviewState = null;
            if (mode == "FAS_INTERVIEW" && conversation.FasSession?.CollectedFactsJson is { Length: > 0 } fasJson)
            {
                var fasData = JsonSerializer.Deserialize<FasInterviewData>(fasJson, JsonOptions);
                if (fasData is not null)
                    interviewState = FasConfirmationService.ToInterviewState(fasData, null);
            }

            IReadOnlyList<AiAction> actions = knowledgeResults.Count > 0
                ? [new("NAVIGATE", "Open FAS application", "/portal/fas")]
                : [];
            IReadOnlyList<AiCard> cards = mode == "PAYMENT"
                ? [new("FINANCE_SUMMARY", new { availableBalance = 0, totalOutstanding = 0, netAvailable = 0, billCount = 0, nearestDueDate = (string?)null })]
                : [];

            var grounding = new AiGrounding(
                knowledgeResults.Count > 0,
                knowledgeResults.Select(r => r.Citation).ToList());
            await WriteSseEventAsync(httpContext, "cards", JsonSerializer.Serialize(cards, JsonOptions), ct);
            await WriteSseEventAsync(httpContext, "actions", JsonSerializer.Serialize(actions, JsonOptions), ct);
            await WriteSseEventAsync(httpContext, "grounding", JsonSerializer.Serialize(grounding, JsonOptions), ct);

            var msg = AiMessage.Create(conversation.Id, "ASSISTANT", _redactor.Redact(finalText), DateTime.UtcNow,
                citationsJson: JsonSerializer.Serialize(grounding.Citations, JsonOptions),
                toolSummaryJson: JsonSerializer.Serialize(cards.Select(x => x.Type), JsonOptions),
                latencyMs: (int)sw.ElapsedMilliseconds,
                responseJson: _redactor.Redact(JsonSerializer.Serialize(new
                {
                    mode,
                    cards,
                    actions,
                    grounding,
                    interviewState
                }, JsonOptions)));
            _db.Add(msg);
            await _db.SaveChangesAsync(ct);

            var donePayload = new { conversationId = conversation.Id, messageId = msg.Id, text = finalText, mode, interviewState };
            await WriteSseEventAsync(httpContext, "done", JsonSerializer.Serialize(donePayload, JsonOptions), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Streaming response failed");
            await WriteSseEventAsync(httpContext, "error", JsonSerializer.Serialize(new { error = "Streaming failed, please retry." }), ct);
        }
    }

    private static async Task WriteSseEventAsync(HttpContext ctx, string eventType, string data, CancellationToken ct)
    {
        await ctx.Response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
}
