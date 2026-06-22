using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Repositories;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModule : IModule
{
    public string Name => "FasPayment";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, FasPaymentModelConfiguration>();
        services.AddScoped<IFasSchemeRepository, FasSchemeRepository>();
        services.AddScoped<ICommandHandler<CreateFasSchemeCommand, CreateFasSchemeResponse>, CreateFasSchemeHandler>();
        services.AddScoped<ICommandHandler<SaveFasSchemeDraftCommand, CreateFasSchemeResponse>, SaveFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<ActivateFasSchemeDraftCommand, CreateFasSchemeResponse>, ActivateFasSchemeDraftHandler>();
        services.AddScoped<ICommandHandler<DeleteFasSchemeDraftCommand, bool>, DeleteFasSchemeDraftHandler>();
        services.AddScoped<IQueryHandler<ListFasSchemesQuery, FasSchemeListResponse>, ListFasSchemesHandler>();
        services.AddScoped<IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>, GetFasSchemeHandler>();
        services.AddScoped<IValidator<CreateFasSchemeRequest>, CreateFasSchemeRequestValidator>();
        services.AddScoped<IValidator<ListFasSchemesRequest>, ListFasSchemesRequestValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
