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
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;

namespace Moe.Modules.EducationAccountTopUp;

public sealed class EducationAccountTopUpModule : IModule
{
    public string Name => "EducationAccountTopUp";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, EducationAccountTopUpModelConfiguration>();
        services.AddScoped<IEducationAccountRepository, EducationAccountRepository>();
        services.AddScoped<IEducationAccountProvisioningGateway, EducationAccountProvisioningGateway>();
        services.AddScoped<IValidator<OpenManualAccountRequest>, OpenManualAccountRequestValidator>();
        services.AddScoped<IValidator<OpenManualAccountCommand>, OpenManualAccountValidator>();
        services.AddScoped<ICommandHandler<OpenManualAccountCommand, OpenManualAccountResponse>, OpenManualAccountHandler>();

        // Top-Up Campaign Commands
        services.AddScoped<ICommandHandler<CreateCampaignCommand, long>, CreateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCampaignCommand>, UpdateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertFixedRecipientsCommand>, UpsertFixedRecipientsCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandHandler>();
        services.AddScoped<ICommandHandler<ExecuteTopUpRunCommand, long>, ExecuteTopUpRunCommandHandler>();

        // Top-Up Campaign Queries
        services.AddScoped<IQueryHandler<PreviewCampaignQuery, PreviewCampaignResult>, PreviewCampaignQueryHandler>();

        // Top-Up Campaign Validators
        services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();
        services.AddScoped<IValidator<UpdateCampaignCommand>, UpdateCampaignCommandValidator>();
        services.AddScoped<IValidator<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandValidator>();
        services.AddScoped<IValidator<UpsertFixedRecipientsCommand>, UpsertFixedRecipientsCommandValidator>();
        services.AddScoped<IValidator<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandValidator>();
        services.AddScoped<IValidator<ExecuteTopUpRunCommand>, ExecuteTopUpRunCommandValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
