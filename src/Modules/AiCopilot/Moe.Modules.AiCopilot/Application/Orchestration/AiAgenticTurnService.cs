using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Domain;
using Moe.Modules.FasPayment.Application.StudentApplications;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiAgenticTurnService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _singletonKernel;
    private readonly AiFinanceReader _finance;
    private readonly StudentFasApplicationService _fasService;
    private readonly IKnowledgeRetriever _knowledge;
    private readonly ILogger<AiAgenticTurnService> _logger;

    public AiAgenticTurnService(Kernel singletonKernel, AiFinanceReader finance, StudentFasApplicationService fasService, IKnowledgeRetriever knowledge, ILogger<AiAgenticTurnService> logger)
    {
        _singletonKernel = singletonKernel;
        _finance = finance;
        _fasService = fasService;
        _knowledge = knowledge;
        _logger = logger;
    }

    public async Task<AiHandlerResult> ExecuteTurnAsync(AiConversation conversation, AiChatRequest request, CancellationToken ct)
    {
        Kernel kernel = _singletonKernel.Clone();
        var plugin = new AiCopilotPlugin(_finance, _fasService, _knowledge) { CurrentConversation = conversation };
        kernel.ImportPluginFromObject(plugin, "AiCopilot");
        IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();

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

You have tools available. Use them when they can help answer the student's question:
- **GetFinanceSnapshotAsync** — call this for balance, bills, payment history, refund queries.
- **SearchKnowledgeBaseAsync** — call this for FAS policy, bursary, subsidy, scheme, document, eligibility process questions.
- **CancelFasInterview** — call this when the student asks to stop/cancel/pause a FAS interview.
- **CheckFasEligibilityAsync** — call this when the student has provided all FAS income/household/nationality facts and wants eligibility results.

Conversation rules:
- Be concise and direct. Use Singapore English.
- Always call the relevant tool before answering — do not guess numbers or policy from memory.
- If no tool answers the question, say you'll connect the student to Admin Centre.
- Never invent policy details.
- Current date: {{DateTime.UtcNow:yyyy-MM-dd}}

Current session context:
- Mode: {{conversation.ModeCode}}
- FAS state: {{fasState}}
- {{modeContext}}
""");
        history.AddUserMessage(request.Message);

        var execSettings = new Microsoft.SemanticKernel.PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };
        string text;
        int iterations = 0;
        while (iterations++ < 8)
        {
            ChatMessageContent answer = await chat.GetChatMessageContentAsync(history,
                executionSettings: execSettings, kernel: kernel, cancellationToken: ct);

            if (!string.IsNullOrEmpty(answer.Content))
            {
                text = answer.Content.Trim();
                string mode = conversation.FasSession?.StatusCode is "COLLECTING" or "CONFIRMING" or "CLARIFYING"
                    ? "FAS_INTERVIEW"
                    : "GENERAL";
                return new AiHandlerResult(text, mode, new(false, []), [], []);
            }

            history.Add(answer);
        }

        throw new InvalidOperationException("Agentic turn exceeded max iterations without producing text response");
    }
}
