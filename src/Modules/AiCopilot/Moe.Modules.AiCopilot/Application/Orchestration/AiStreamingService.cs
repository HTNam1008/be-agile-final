using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moe.Modules.AiCopilot.Api;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiStreamingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<AiStreamingService> _logger;

    public AiStreamingService(ILogger<AiStreamingService> logger)
    {
        _logger = logger;
    }

    public async Task StreamResponseAsync(HttpContext httpContext, AiChatResponse response, CancellationToken ct)
    {
        await WriteSseEventAsync(httpContext, "cards", JsonSerializer.Serialize(response.Cards, JsonOptions), ct);
        await WriteSseEventAsync(httpContext, "actions", JsonSerializer.Serialize(response.Actions, JsonOptions), ct);
        await WriteSseEventAsync(httpContext, "grounding", JsonSerializer.Serialize(response.Grounding, JsonOptions), ct);

        await WriteSseEventAsync(httpContext, "text", response.Text, ct);

        var donePayload = new
        {
            conversationId = response.ConversationId,
            messageId = response.MessageId,
            text = response.Text,
            mode = response.Mode,
            interviewState = response.InterviewState,
            followUpQuestions = response.FollowUpQuestions,
            turnIntent = response.TurnIntent,
            conversationPhase = response.ConversationPhase,
            reviewRecordId = response.ReviewRecordId
        };
        await WriteSseEventAsync(httpContext, "done", JsonSerializer.Serialize(donePayload, JsonOptions), ct);
    }

    public async Task WriteErrorEventAsync(HttpContext httpContext, string errorMessage, CancellationToken ct)
    {
        await WriteSseEventAsync(httpContext, "error", JsonSerializer.Serialize(new { error = errorMessage }), ct);
    }

    private static async Task WriteSseEventAsync(HttpContext ctx, string eventType, string data, CancellationToken ct)
    {
        await ctx.Response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
}
