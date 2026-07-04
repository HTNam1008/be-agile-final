using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiAgenticTurnService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _kernel;
    private readonly ILogger<AiAgenticTurnService> _logger;

    public AiAgenticTurnService(Kernel kernel, ILogger<AiAgenticTurnService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<AiHandlerResult> ExecuteTurnAsync(AiConversation conversation, AiChatRequest request, CancellationToken ct)
    {
        try
        {
            IChatCompletionService chat = _kernel.GetRequiredService<IChatCompletionService>();

            string fasState = conversation.FasSession?.StatusCode switch
            {
                "COLLECTING" => "FAS interview in progress — collecting facts. Current field: " +
                    (conversation.FasSession.NextQuestion ?? "unknown"),
                "CONFIRMING" => "FAS interview at confirmation step — all facts collected, awaiting yes/no.",
                "CLARIFYING" => "FAS interview needs clarification on the last answer.",
                "COMPLETE" => "FAS eligibility check completed.",
                "PAUSED" => "FAS check is paused. User can resume by asking.",
                "CANCELLED" => "FAS check was cancelled.",
                _ => "No active FAS session."
            };

            string modeContext = request.PageContext?.Domain is not null
                ? $"Page domain: {request.PageContext.Domain}. Path: {request.PageContext.Path ?? "none"}."
                : "No page context.";

            var history = new ChatHistory($$"""
You are the MOE Student Finance AI Copilot for Singapore's Ministry of Education.

Available tools:
1. **GetFinanceSnapshotAsync** — Get Education Account balance, outstanding bills, payment history.
2. **SearchKnowledgeBaseAsync** — Search FAS policy documents for guidance on bursaries, subsidies, eligibility.
3. **CancelFasInterviewAsync** — Cancel or pause an active FAS interview.

Conversation rules:
- Be concise and direct. Use Singapore English.
- For payment/billing questions, call GetFinanceSnapshotAsync.
- For policy questions, call SearchKnowledgeBaseAsync.
- If no tool answers the question, say you'll connect the student to Admin Centre.
- Never invent policy details.
- Current date: {{DateTime.UtcNow:yyyy-MM-dd}}

Current session context:
- Mode: {{conversation.ModeCode}}
- FAS state: {{fasState}}
- {{modeContext}}
""");
            history.AddUserMessage(request.Message);

            ChatMessageContent answer = await chat.GetChatMessageContentAsync(history, kernel: _kernel, cancellationToken: ct);
            string text = answer.Content?.Trim() ?? "I'm not sure how to help with that.";

            string mode = conversation.FasSession?.StatusCode is "COLLECTING" or "CONFIRMING" or "CLARIFYING"
                ? "FAS_INTERVIEW"
                : "GENERAL";

            return new AiHandlerResult(text, mode, new(false, []), [], []);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agentic turn failed — falling back to deterministic path");
            throw;
        }
    }
}
