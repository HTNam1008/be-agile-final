using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
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
        
        services.AddScoped<Moe.Application.Abstractions.Messaging.IQueryHandler<Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications.GetSchemeApplicationsQuery, Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications.GetSchemeApplicationsResponse>, Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications.GetSchemeApplicationsHandler>();
        services.AddScoped<Moe.Application.Abstractions.Messaging.IQueryHandler<Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail.GetApplicationDetailQuery, Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail.GetApplicationDetailResponse>, Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail.GetApplicationDetailHandler>();
        services.AddScoped<Moe.Application.Abstractions.Messaging.ICommandHandler<Moe.Modules.FasPayment.Application.Applications.Approve.ApproveApplicationCommand, Moe.Modules.FasPayment.Application.Applications.Approve.ApproveApplicationResponse>, Moe.Modules.FasPayment.Application.Applications.Approve.ApproveApplicationHandler>();
        services.AddScoped<Moe.Application.Abstractions.Messaging.ICommandHandler<Moe.Modules.FasPayment.Application.Applications.Reject.RejectApplicationCommand, Moe.Modules.FasPayment.Application.Applications.Reject.RejectApplicationResponse>, Moe.Modules.FasPayment.Application.Applications.Reject.RejectApplicationHandler>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
