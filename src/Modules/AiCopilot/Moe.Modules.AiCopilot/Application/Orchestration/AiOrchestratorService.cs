using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Moe.Modules.AiCopilot.Application.Orchestration;

public sealed class AiOrchestratorService(Kernel kernel)
{
    public async Task<string> ChatAsync(string userMessage, object pageContext, CancellationToken ct)
    {
        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();

        var chunks = Infrastructure.RAG.MockDocumentIngestor.GetChunks();
        var contextStr = string.Join("\n\n", chunks.Select(c => $"[CONTEXT: {c.DocName} - {c.Section}]\n{c.Content}"));

        history.AddSystemMessage($"""
You are an AI assistant for the MOE Student Finance portal. Help users with FAS applications and payment queries.
You must use the following context to answer questions about policy. If the context does not contain the answer, politely decline and provide a 'Contact Admin Center' recommendation.

{contextStr}
""");
        history.AddUserMessage(userMessage);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var response = await chatCompletion.GetChatMessageContentAsync(history, executionSettings, kernel, ct);
        return response.Content ?? string.Empty;
    }
}
