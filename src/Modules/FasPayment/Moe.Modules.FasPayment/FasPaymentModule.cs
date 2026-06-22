using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Repositories;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModule : IModule
{
    public string Name => "FasPayment";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, FasPaymentModelConfiguration>();
        services.AddScoped<IFasApplicationRepository, FasApplicationRepository>();

        services.AddScoped<IQueryHandler<GetSchemeApplicationsQuery, GetSchemeApplicationsResponse>, GetSchemeApplicationsHandler>();
        services.AddScoped<IQueryHandler<GetApplicationDetailQuery, GetApplicationDetailResponse>, GetApplicationDetailHandler>();
        services.AddScoped<ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>, ApproveApplicationHandler>();
        services.AddScoped<ICommandHandler<RejectApplicationCommand, RejectApplicationResponse>, RejectApplicationHandler>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
