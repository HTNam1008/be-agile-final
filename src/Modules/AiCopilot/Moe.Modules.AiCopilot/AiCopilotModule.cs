using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.AiCopilot.Application.Finance;
using Moe.Modules.AiCopilot.Application.Knowledge;
using Moe.Modules.AiCopilot.Application.Orchestration;
using Moe.Modules.AiCopilot.Application.Reviews;
using Moe.Modules.AiCopilot.Application.Security;
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
            string chatDeployment = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:ChatDeploymentName is required for AI requests.");
            string? embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeploymentName"];
            IKernelBuilder builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(chatDeployment, endpoint, apiKey);
            if (embeddingDeployment is not null)
            {
                builder.AddAzureOpenAITextEmbeddingGeneration(embeddingDeployment, endpoint, apiKey);
            }
            return builder.Build();
        });
        services.AddScoped<AiOrchestratorService>();
        services.AddScoped<AiTurnPlannerService>();
        services.AddScoped<AiFinanceReader>();
        services.AddScoped<FallbackHandler>();
        services.AddScoped<PaymentQueryHandler>();
        services.AddScoped<KnowledgeAnswerHandler>();
        services.AddScoped<FasInterviewHandler>();
        string knowledgeStore = configuration.GetValue("AiCopilot:KnowledgeStore", "Embedded");
        if (knowledgeStore == "Database")
        {
            services.AddScoped<IKnowledgeDocumentStore, DatabaseKnowledgeDocumentStore>();
        }
        else
        {
            services.AddSingleton<IKnowledgeDocumentStore, EmbeddedKnowledgeDocumentStore>();
        }
        services.AddSingleton<IKnowledgeRetriever, LocalKnowledgeRetriever>();
        services.AddSingleton<SensitiveDataRedactor>();
        services.AddSingleton<IModelConfigurationContributor, AiModelConfiguration>();
        services.AddScoped<AiReviewService>();
        if (IsBackgroundJobEnabled(configuration, "AiCopilot:Retention"))
        {
            services.AddHostedService<AiRetentionService>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }

    private static bool IsBackgroundJobEnabled(IConfiguration configuration, string key)
        => configuration.GetValue("BackgroundJobs:Enabled", true)
           && configuration.GetValue($"BackgroundJobs:{key}", true);
}
