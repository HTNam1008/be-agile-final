using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Api.Admin;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;

namespace Moe.Modules.EducationAccountTopUp;

public sealed class EducationAccountTopUpModule : IModule
{
    public string Name => "EducationAccountTopUp";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, EducationAccountTopUpModelConfiguration>();
        services.AddScoped<IEducationAccountRepository, EducationAccountRepository>();
        services.AddScoped<ITopUpCampaignRepository, TopUpCampaignRepository>();
        services.AddScoped<ITopUpRunRepository, TopUpRunRepository>();
        services.AddScoped<ITopUpTransactionRepository, TopUpTransactionRepository>();
        services.AddSingleton<ITopUpRunDispatcher, InProcessTopUpRunDispatcher>();
        services.AddScoped<ITopUpAccountProjectionRepository, TopUpAccountProjectionRepository>();
        services.AddScoped<IEducationAccountProvisioningGateway, EducationAccountProvisioningGateway>();
        services.AddScoped<IValidator<OpenManualAccountRequest>, OpenManualAccountRequestValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsRequest>, SearchTopUpAccountsRequestValidator>();
        services.AddScoped<IValidator<OpenManualAccountCommand>, OpenManualAccountValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsQuery>, SearchTopUpAccountsValidator>();
        services.AddScoped<ICommandHandler<OpenManualAccountCommand, OpenManualAccountResponse>, OpenManualAccountHandler>();
        services.AddScoped<IValidator<RequestManualRunRequest>, RequestManualRunRequestValidator>();
        services.AddScoped<IValidator<RequestManualRunCommand>, RequestManualRunCommandValidator>();
        services.AddScoped<ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>, RequestManualRunCommandHandler>();
        services.AddScoped<IQueryHandler<SearchTopUpAccountsQuery, SearchTopUpAccountsResponse>, SearchTopUpAccountsHandler>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
