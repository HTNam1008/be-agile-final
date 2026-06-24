using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Moe.Application.Abstractions.Modules;

namespace Moe.Modules.AiCopilot;

public sealed class AiCopilotModule : IModule
{
    public string Name => "AiCopilot";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
        var apiKey = configuration["AzureOpenAI:ApiKey"] ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is required.");
        var deploymentName = configuration["AzureOpenAI:ChatDeploymentName"] ?? throw new InvalidOperationException("AzureOpenAI:ChatDeploymentName is required.");

        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

        services.AddSingleton(sp => kernelBuilder.Build());
        services.AddScoped<Moe.Modules.AiCopilot.Application.Orchestration.AiOrchestratorService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
