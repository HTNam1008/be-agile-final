using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Api.Admin;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;
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
        services.AddScoped<ITopUpAccountProjectionRepository, TopUpAccountProjectionRepository>();
        services.AddScoped<ITopUpCampaignRepository, TopUpCampaignRepository>();
        services.AddScoped<ITopUpRunRepository, TopUpRunRepository>();
        services.AddScoped<ITopUpTransactionRepository, TopUpTransactionRepository>();
        services.AddScoped<ITopUpRunDispatcher, InProcessTopUpRunDispatcher>();
        services.AddScoped<IEducationAccountProvisioningGateway, EducationAccountProvisioningGateway>();
        services.AddScoped<IValidator<OpenManualAccountRequest>, OpenManualAccountRequestValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsRequest>, SearchTopUpAccountsRequestValidator>();
        services.AddScoped<IValidator<UpsertFixedRecipientsRequest>, UpsertFixedRecipientsRequestValidator>();
        services.AddScoped<IValidator<OpenManualAccountCommand>, OpenManualAccountValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsQuery>, SearchTopUpAccountsValidator>();
        services.AddScoped<IValidator<TopUpAccountSelection>, TopUpAccountSelectionValidator>();
        services.AddScoped<ITopUpAccountSelectionResolver, TopUpAccountSelectionResolver>();
        services.AddScoped<ICommandHandler<OpenManualAccountCommand, OpenManualAccountResponse>, OpenManualAccountHandler>();
        // Top-Up Campaign Commands
        services.AddScoped<ICommandHandler<CreateCampaignCommand, long>, CreateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCampaignCommand>, UpdateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertFixedRecipientsCommand, UpsertFixedRecipientsResponse>, UpsertFixedRecipientsCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandHandler>();
        services.AddScoped<ICommandHandler<ExecuteTopUpRunCommand, long>, ExecuteTopUpRunCommandHandler>();
        services.AddScoped<ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>, RequestManualRunCommandHandler>();

        // Top-Up Campaign Queries
        services.AddScoped<IQueryHandler<PreviewCampaignQuery, PreviewCampaignResult>, PreviewCampaignQueryHandler>();

        // Top-Up Campaign Validators
        services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();
        services.AddScoped<IValidator<UpdateCampaignCommand>, UpdateCampaignCommandValidator>();
        services.AddScoped<IValidator<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandValidator>();
        services.AddScoped<IValidator<UpsertFixedRecipientsCommand>, UpsertFixedRecipientsCommandValidator>();
        services.AddScoped<IValidator<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandValidator>();
        services.AddScoped<IValidator<ExecuteTopUpRunCommand>, ExecuteTopUpRunCommandValidator>();
        services.AddScoped<IValidator<RequestManualRunRequest>, RequestManualRunRequestValidator>();
        services.AddScoped<IValidator<RequestManualRunCommand>, RequestManualRunCommandValidator>();
        services.AddScoped<ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>, RequestManualRunCommandHandler>();
        
        services.AddScoped<IQueryHandler<SearchTopUpAccountsQuery, SearchTopUpAccountsResponse>, SearchTopUpAccountsHandler>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
