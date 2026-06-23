using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Application.AdminFasSchemes;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.Application.Applications.Approve;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.Reject;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Repositories;
using Moe.Modules.FasPayment.Application.StudentApplications;
using Moe.Modules.FasPayment.Infrastructure.Documents;
using Moe.Modules.FasPayment.Api;

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
        services.AddScoped<ICommandHandler<PublishFasSchemeCommand, CreateFasSchemeResponse>, PublishFasSchemeHandler>();
        services.AddScoped<ICommandHandler<DisableFasSchemeCommand, CreateFasSchemeResponse>, DisableFasSchemeHandler>();
        services.AddScoped<ICommandHandler<DeleteFasSchemeCommand, CreateFasSchemeResponse>, DeleteFasSchemeHandler>();
        services.AddScoped<IQueryHandler<ListFasSchemesQuery, FasSchemeListResponse>, ListFasSchemesHandler>();
        services.AddScoped<IQueryHandler<GetFasSchemeQuery, FasSchemeDetail>, GetFasSchemeHandler>();
        services.AddScoped<IValidator<CreateFasSchemeRequest>, CreateFasSchemeRequestValidator>();
        services.AddScoped<IValidator<ListFasSchemesRequest>, ListFasSchemesRequestValidator>();
        services.AddScoped<IFasApplicationRepository, FasApplicationRepository>();
        services.AddScoped<StudentFasApplicationService>();
        services.AddScoped<FasApiExceptionFilter>();
        services.AddSingleton<IFasDocumentStorage>(sp => string.IsNullOrWhiteSpace(configuration["FasDocuments:AzureBlobConnectionString"])
            ? new PrivateFileFasDocumentStorage()
            : new AzureBlobFasDocumentStorage(configuration));
        services.AddSingleton<IFasDocumentScanner, ConfiguredFasDocumentScanner>();

        services.AddScoped<IQueryHandler<GetSchemeApplicationsQuery, GetSchemeApplicationsResponse>, GetSchemeApplicationsHandler>();
        services.AddScoped<IQueryHandler<GetApplicationDetailQuery, GetApplicationDetailResponse>, GetApplicationDetailHandler>();
        services.AddScoped<ICommandHandler<ApproveApplicationCommand, ApproveApplicationResponse>, ApproveApplicationHandler>();
        services.AddScoped<ICommandHandler<RejectApplicationCommand, RejectApplicationResponse>, RejectApplicationHandler>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
