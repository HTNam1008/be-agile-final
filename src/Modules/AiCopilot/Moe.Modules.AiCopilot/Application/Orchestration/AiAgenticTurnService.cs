using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiAgenticTurnService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _config;
    private readonly AiCopilotPlugin _plugin;
    private readonly ILogger<AiAgenticTurnService> _logger;

    public AiAgenticTurnService(IConfiguration config, AiCopilotPlugin plugin, ILogger<AiAgenticTurnService> logger)
    {
        _config = config;
        _plugin = plugin;
        _logger = logger;
    }

    public async Task<AiHandlerResult> ExecuteTurnAsync(AiConversation conversation, AiChatRequest request, CancellationToken ct)
    {
        try
        {
            string endpoint = _config["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint required");
            string apiKey = _config["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey required");
            string deployment = _config["AzureOpenAI:ChatDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:ChatDeploymentName required");
            var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
            Kernel kernel = builder.Build();
            kernel.ImportPluginFromObject(_plugin, "AiCopilot");
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
- **GetProfileFacts** — call this to retrieve student profile facts for FAS prefill (email, nationality, institution).
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
            while (true)
            {
                ChatMessageContent answer = await chat.GetChatMessageContentAsync(history,
                    executionSettings: execSettings, kernel: kernel, cancellationToken: ct);

                // If the assistant sent text content (not a tool call), we're done
                if (!string.IsNullOrEmpty(answer.Content))
                {
                    text = answer.Content.Trim();
                    break;
                }

                // Assistant made tool calls — SK auto-invoked them and added results
                // to history. Add the assistant's message so the next LLM call sees
                // both the function call and its result.
                history.Add(answer);
            }

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
