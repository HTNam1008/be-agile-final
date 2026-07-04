using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moe.Modules.AiCopilot.Api;
using Moe.Modules.AiCopilot.Domain;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiAgenticTurnService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Kernel _kernel;
    private readonly ILogger<AiAgenticTurnService> _logger;
    private static readonly string SystemPrompt = $$"""
You are the MOE Student Finance AI Copilot for Singapore's Ministry of Education.

Available tools:
1. **GetFinanceSnapshotAsync** — Get Education Account balance, outstanding bills, payment history. Always use this when the student asks about bills, payments, balance, refunds, or account.
2. **SearchKnowledgeBaseAsync** — Search FAS policy documents for guidance on bursaries, subsidies, eligibility, fees, applications, documents. Use this for policy questions.
3. **CancelFasInterviewAsync** — Cancel or pause an active FAS interview.

Conversation rules:
- Be concise and direct. Use Singapore English.
- Always cite your sources when giving policy information.
- If the student asks about FAS eligibility, guide them to start the FAS wizard.
- For payment/billing questions, call GetFinanceSnapshotAsync.
- For policy questions, call SearchKnowledgeBaseAsync.
- Never invent policy details — always search the knowledge base first.
- If you cannot answer, suggest contacting the Admin Centre.

Current date: {{DateTime.UtcNow:yyyy-MM-dd}}
""";

    public AiAgenticTurnService(Kernel kernel, ILogger<AiAgenticTurnService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    public async Task<AiHandlerResult> ExecuteTurnAsync(AiConversation conversation, AiChatRequest request, CancellationToken ct)
    {
        try
        {
            KernelArguments args = new()
            {
                ["request"] = request.Message,
                ["mode"] = conversation.ModeCode,
                ["domain"] = request.PageContext?.Domain ?? "GENERAL"
            };

            FunctionResult result = await _kernel.InvokePromptAsync(request.Message, args, cancellationToken: ct);

            string text = result.GetValue<string>() ?? "I'm not sure how to help with that.";

            return new AiHandlerResult(text, DetermineMode(text, conversation), new(false, []), [], []);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agentic turn failed — falling back to deterministic path");
            throw;
        }
    }

    private static string DetermineMode(string response, AiConversation conversation)
    {
        if (conversation.FasSession is not null &&
            conversation.FasSession.StatusCode is "COLLECTING" or "CONFIRMING" or "CLARIFYING")
            return "FAS_INTERVIEW";
        return "GENERAL";
    }
}
