using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;

namespace Moe.Modules.CourseBilling;

public sealed class CourseBillingModule : IModule
{
    public string Name => "CourseBilling";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IModelConfigurationContributor, CourseBillingModelConfiguration>();
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
