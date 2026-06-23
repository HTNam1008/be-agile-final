using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Api.Admin;
using Moe.Modules.EducationAccountTopUp.Application.EducationAccounts.GetMyEducationAccount;
using Moe.Modules.EducationAccountTopUp.Application.History;
using Moe.Modules.EducationAccountTopUp.Application.History.CampaignHistory;
using Moe.Modules.EducationAccountTopUp.Application.History.RunHistory;
using Moe.Modules.EducationAccountTopUp.Application.OpenAccount;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.GetRunSummary;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.AccountSelection;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ChangeCampaignStatus;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.CreateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetCampaigns;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.GetFixedRecipients;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.PreviewCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.SearchAccounts;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpdateCampaign;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertCampaignRules;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.UpsertFixedRecipients;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.AdminDashboard;
using Moe.Modules.EducationAccountTopUp.IGateway.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.History;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.IGateway.RunSummary;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Infrastructure.AdminDashboard;
using Moe.Modules.EducationAccountTopUp.Infrastructure.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;
using Moe.Modules.EducationAccountTopUp.Infrastructure.History;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;
using Moe.Modules.EducationAccountTopUp.Infrastructure.RunSummary;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUpRunDispatcher;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUps;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TransactionResults;
using Moe.Modules.IdentityPlatform.IGateway.Accounts;

namespace Moe.Modules.EducationAccountTopUp;

public sealed class EducationAccountTopUpModule : IModule
{
    public string Name => "EducationAccountTopUp";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, EducationAccountTopUpModelConfiguration>();
        // Gateways & Repositories
        services.AddScoped<IEducationAccountRepository, EducationAccountRepository>();
        services.AddScoped<ITopUpCampaignRepository, TopUpCampaignRepository>();
        services.AddScoped<ITopUpRunRepository, TopUpRunRepository>();
        services.AddScoped<ITopUpTransactionRepository, TopUpTransactionRepository>();
        services.AddScoped<IAccountCreditGateway, AccountCreditGateway>();
        services.AddScoped<ITopUpAccountProjectionRepository, TopUpAccountProjectionRepository>();
        services.AddScoped<ITopUpCampaignReader, TopUpCampaignReader>();
        services.AddScoped<IEducationAccountReader, EducationAccountReader>();
        services.AddScoped<ITopUpHistoryReader, TopUpHistoryReader>();
        services.AddScoped<ITopUpRunSummaryReader, TopUpRunSummaryReader>();
        services.AddScoped<ITopUpTransactionResultsReader, TopUpTransactionResultsReader>();
        services.AddScoped<IEducationAccountProvisioningGateway, EducationAccountProvisioningGateway>();
        services.AddScoped<IEducationAccountDirectory, EducationAccountDirectory>();
        services.AddScoped<IEducationAccountPaymentGateway, EducationAccountPaymentGateway>();
        services.AddScoped<IAdminDashboardTopUpDirectory, AdminDashboardTopUpDirectory>();

        // Services & Utilities
        services.AddSingleton<ChannelTopUpRunDispatcher>();
        services.AddSingleton<ITopUpRunDispatcher>(sp => sp.GetRequiredService<ChannelTopUpRunDispatcher>());
        services.AddSingleton<ITopUpRunQueueReader>(sp => sp.GetRequiredService<ChannelTopUpRunDispatcher>());
        services.AddScoped<IRecipientValidator, StubRecipientValidator>();
        services.AddScoped<IRecipientResolver, TopUpRecipientResolver>();
        services.AddScoped<ITopUpExecutionEventPublisher, LoggingTopUpExecutionEventPublisher>();
        services.AddSingleton<ITopUpExecutionMetrics, TopUpExecutionMetrics>();
        services.AddScoped<IDynamicRuleFilter, DynamicRuleFilter>();
        services.AddScoped<ITopUpAccessScopeResolver, TopUpAccessScopeResolver>();
        services.AddScoped<IRecipientProcessingService, RecipientProcessingService>();
        services.AddScoped<IRunExecutionOrchestrator, RunExecutionOrchestrator>();
        services.AddScoped<IRunReconciliationService, RunReconciliationService>();
        services.AddScoped<IPendingTransactionRecoveryService, PendingTransactionRecoveryService>();
        // Workers
        services.AddHostedService<TopUpRunWorker>();
        services.AddHostedService<TopUpSchedulerWorker>();
        services.AddScoped<ITopUpAccountSelectionResolver, TopUpAccountSelectionResolver>();

        // Commands
        services.AddScoped<ICommandHandler<CreateCampaignCommand, long>, CreateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCampaignCommand>, UpdateCampaignCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertFixedRecipientsCommand, UpsertFixedRecipientsResponse>, UpsertFixedRecipientsCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandHandler>();
        services.AddScoped<ICommandHandler<RequestManualRunCommand, RequestManualRunResponse>, RequestManualRunCommandHandler>();
        services.AddScoped<ICommandHandler<OpenManualAccountCommand, OpenManualAccountResponse>, OpenManualAccountHandler>();

        // Queries
        services.AddScoped<IQueryHandler<GetMyEducationAccountQuery, MyEducationAccountDto>, GetMyEducationAccountQueryHandler>();
        services.AddScoped<IQueryHandler<GetMyEducationAccountTransactionsQuery, MyEducationAccountTransactionsPage>, GetMyEducationAccountTransactionsQueryHandler>();
        services.AddScoped<IQueryHandler<GetCampaignsQuery, IReadOnlyList<CampaignListItem>>, GetCampaignsQueryHandler>();
        services.AddScoped<IQueryHandler<GetCampaignRulesQuery, IReadOnlyList<CampaignRuleDto>>, GetCampaignRulesQueryHandler>();
        services.AddScoped<IQueryHandler<GetFixedRecipientsQuery, IReadOnlyList<FixedRecipientDto>>, GetFixedRecipientsQueryHandler>();
        services.AddScoped<IQueryHandler<PreviewCampaignQuery, PreviewCampaignResult>, PreviewCampaignQueryHandler>();
        services.AddScoped<IQueryHandler<GetRunSummaryQuery, RunSummaryResponse>, GetRunSummaryQueryHandler>();
        services.AddScoped<IQueryHandler<GetTopUpTransactionResultsQuery, PageResponse<TopUpTransactionResultItem>>, GetTopUpTransactionResultsHandler>();
        services.AddScoped<IQueryHandler<GetCampaignHistoryQuery, PageResponse<CampaignHistoryItem>>, GetCampaignHistoryHandler>();
        services.AddScoped<IQueryHandler<GetRunHistoryQuery, PageResponse<RunHistoryItem>>, GetRunHistoryHandler>();
        services.AddScoped<IQueryHandler<SearchTopUpAccountsQuery, SearchTopUpAccountsResponse>, SearchTopUpAccountsHandler>();

        // Validators
        services.AddScoped<IValidator<OpenManualAccountRequest>, OpenManualAccountRequestValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsRequest>, SearchTopUpAccountsRequestValidator>();
        services.AddScoped<IValidator<UpsertFixedRecipientsRequest>, UpsertFixedRecipientsRequestValidator>();
        services.AddScoped<IValidator<CampaignHistoryRequest>, CampaignHistoryRequestValidator>();
        services.AddScoped<IValidator<RunHistoryRequest>, RunHistoryRequestValidator>();
        services.AddScoped<IValidator<TopUpTransactionResultsRequest>, TopUpTransactionResultsRequestValidator>();
        services.AddScoped<IValidator<OpenManualAccountCommand>, OpenManualAccountValidator>();
        services.AddScoped<IValidator<SearchTopUpAccountsQuery>, SearchTopUpAccountsValidator>();
        services.AddScoped<IValidator<TopUpAccountSelection>, TopUpAccountSelectionValidator>();
        services.AddScoped<IValidator<CreateCampaignCommand>, CreateCampaignCommandValidator>();
        services.AddScoped<IValidator<UpdateCampaignCommand>, UpdateCampaignCommandValidator>();
        services.AddScoped<IValidator<ChangeCampaignStatusCommand>, ChangeCampaignStatusCommandValidator>();
        services.AddScoped<IValidator<UpsertFixedRecipientsCommand>, UpsertFixedRecipientsCommandValidator>();
        services.AddScoped<IValidator<UpsertCampaignRulesCommand>, UpsertCampaignRulesCommandValidator>();
        services.AddScoped<IValidator<RequestManualRunRequest>, RequestManualRunRequestValidator>();
        services.AddScoped<IValidator<RequestManualRunCommand>, RequestManualRunCommandValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
