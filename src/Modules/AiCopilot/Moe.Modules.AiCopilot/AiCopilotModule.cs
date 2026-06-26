using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Application.Reviews;
using Moe.Modules.AiCopilot.Infrastructure.Knowledge;
using Moe.Modules.AiCopilot.Infrastructure.Persistence;

namespace Moe.Modules.AiCopilot;

public sealed class AiCopilotModule : IModule
{
    public string Name => "AiCopilot";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            string endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required for AI requests.");
            string apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required for AI requests.");
            string deploymentName = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:ChatDeploymentName is required for AI requests.");
            return Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey).Build();
        });
        services.AddScoped<Moe.Modules.AiCopilot.Application.Orchestration.AiOrchestratorService>();
        services.AddScoped<AiFinanceReader>();
        string? fasKnowledgeDir = configuration["KnowledgeBase:FasDirectory"];
        if (string.IsNullOrEmpty(fasKnowledgeDir))
            fasKnowledgeDir = LocalKnowledgeRetriever.FindDefaultFasDirectory();
        var fasChunks = fasKnowledgeDir != null
            ? LocalKnowledgeRetriever.LoadFasChunks(fasKnowledgeDir)
            : [];
        services.AddSingleton<IKnowledgeRetriever>(_ => new LocalKnowledgeRetriever(fasChunks));
        services.AddSingleton<SensitiveDataRedactor>();
        services.AddSingleton<IModelConfigurationContributor, AiModelConfiguration>();
        services.AddScoped<AiReviewService>();
        services.AddHostedService<AiRetentionService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
