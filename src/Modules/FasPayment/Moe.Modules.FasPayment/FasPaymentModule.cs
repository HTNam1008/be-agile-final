using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModule : IModule
{
    public string Name => "FasPayment";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IModelConfigurationContributor, FasPaymentModelConfiguration>();
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
