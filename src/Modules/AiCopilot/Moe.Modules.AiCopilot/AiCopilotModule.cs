using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
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
        string endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required for AI requests.");
        string apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required for AI requests.");
        string chatDeployment = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:ChatDeploymentName is required for AI requests.");
        services.AddSingleton(_ => Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(chatDeployment, endpoint, apiKey).Build());
        string? embeddingDeployment = configuration["AzureOpenAI:EmbeddingDeploymentName"];
        if (embeddingDeployment is not null)
        {
            IKernelBuilder embedBuilder = Kernel.CreateBuilder();
            embedBuilder.AddAzureOpenAITextEmbeddingGeneration(embeddingDeployment, endpoint, apiKey);
            Kernel embedKernel = embedBuilder.Build();
            services.AddSingleton<ITextEmbeddingGenerationService>(_ => embedKernel.GetRequiredService<ITextEmbeddingGenerationService>());
        }
        services.AddScoped<AiTurnRouter>();
        services.AddScoped<AiTurnPlannerService>();
        services.AddScoped<AiFinanceReader>();
        services.AddScoped<FallbackHandler>();
        services.AddScoped<PaymentQueryHandler>();
        services.AddScoped<KnowledgeAnswerHandler>();
        services.AddScoped<FasInterviewHandler>();
        services.AddScoped<FasExtractionService>();
        services.AddScoped<FasEligibilityService>();
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
        services.AddScoped<AiCopilotPlugin>();
        services.AddScoped<AiAgenticTurnService>();
        services.AddScoped<AiStreamingService>();
        services.AddSingleton<IModelConfigurationContributor, AiModelConfiguration>();
        services.AddScoped<AiReviewService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }

}
