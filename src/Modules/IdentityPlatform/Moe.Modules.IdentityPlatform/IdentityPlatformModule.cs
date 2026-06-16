using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.IdentityPlatform.Application.Access.AssignAccessScope;
using Moe.Modules.IdentityPlatform.Api.Admin;
using Moe.Modules.IdentityPlatform.Application.Access.RevokeAccessScope;
using Moe.Modules.IdentityPlatform.Application.AdminUsers.CreateAdminUser;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;
using Moe.Modules.IdentityPlatform.Application.Authentication.GetEServiceAuthFlow;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.DisableUserAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.ProvisionStudentSingpassAccount;
using Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.RetryIdentityProvisioning;
using Moe.Modules.IdentityPlatform.Infrastructure.Bootstrap;
using Moe.Modules.IdentityPlatform.Infrastructure.EntraWorkforce;
using Moe.Modules.IdentityPlatform.Infrastructure.Authentication;
using Moe.Modules.IdentityPlatform.Infrastructure.People;
using Moe.Modules.IdentityPlatform.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.Infrastructure.Singpass;
using Moe.Modules.IdentityPlatform.IGateway.Admin;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;

namespace Moe.Modules.IdentityPlatform;

public sealed class IdentityPlatformModule : IModule
{
    public string Name => "IdentityPlatform";
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, IdentityPlatformModelConfiguration>();
        services.AddOptions<AdminBootstrapOptions>().BindConfiguration(AdminBootstrapOptions.SectionName);
        services.AddOptions<EntraWorkforceDirectoryOptions>().BindConfiguration(EntraWorkforceDirectoryOptions.SectionName);
        services.AddMemoryCache();
        services.AddHostedService<AdminBootstrapHostedService>();
        services.AddScoped<IClaimsTransformation, LocalClaimsTransformation>();
        services.AddHttpClient<IEntraWorkforceDirectoryClient, EntraWorkforceDirectoryClient>();
        services.AddHttpClient<ISingpassLoginGateway, MockPassFapiLoginGateway>();
        services.AddScoped<ILocalIdentityDirectory, LocalIdentityDirectory>();
        services.AddScoped<IPersonDirectory, PersonDirectory>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IExternalIdentityProvisioningRepository, UserAccountRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IAccessScopeRepository, AccessScopeRepository>();
        services.AddScoped<IIdentityProvisioningRequestRepository, IdentityProvisioningRequestRepository>();
        services.AddScoped<IStudentSingpassProvisioningRepository, StudentSingpassProvisioningRepository>();
        services.AddScoped<ILocalIdentityRepository, LocalIdentityRepository>();
        services.AddScoped<IQueryHandler<GetAdminAuthFlowQuery, AdminAuthFlowResponse>, GetAdminAuthFlowHandler>();
        services.AddScoped<IQueryHandler<GetCurrentIdentityQuery, LocalIdentitySummary>, GetCurrentIdentityHandler>();
        services.AddScoped<IQueryHandler<GetEServiceAuthFlowQuery, EServiceAuthFlowResponse>, GetEServiceAuthFlowHandler>();
        services.AddScoped<ICommandHandler<CreateAdminUserCommand, CreateAdminUserResponse>, CreateAdminUserHandler>();
        services.AddScoped<IQueryHandler<GetIdentityProvisioningRequestQuery, IdentityProvisioningRequestResponse>, GetIdentityProvisioningRequestHandler>();
        services.AddScoped<ICommandHandler<AssignAccessScopeCommand, AssignAccessScopeResponse>, AssignAccessScopeHandler>();
        services.AddScoped<ICommandHandler<DisableUserAccountCommand, DisableUserAccountResponse>, DisableUserAccountHandler>();
        services.AddScoped<ICommandHandler<ProvisionStudentSingpassAccountCommand, ProvisionStudentSingpassAccountResponse>, ProvisionStudentSingpassAccountHandler>();
        services.AddScoped<ICommandHandler<RetryIdentityProvisioningCommand, IdentityProvisioningRequestResponse>, RetryIdentityProvisioningHandler>();
        services.AddScoped<ICommandHandler<RevokeAccessScopeCommand, RevokeAccessScopeResponse>, RevokeAccessScopeHandler>();
        services.AddScoped<IValidator<AssignAccessScopeCommand>, AssignAccessScopeValidator>();
        services.AddScoped<IValidator<CreateAdminUserCommand>, CreateAdminUserValidator>();
        services.AddScoped<IValidator<ProvisionStudentSingpassAccountCommand>, ProvisionStudentSingpassAccountValidator>();
        services.AddScoped<IValidator<AssignAccessScopeRequest>, AssignAccessScopeRequestValidator>();
        services.AddScoped<IValidator<CreateAdminUserRequest>, CreateAdminUserRequestValidator>();
        services.AddScoped<IValidator<ProvisionStudentSingpassAccountRequest>, ProvisionStudentSingpassAccountRequestValidator>();
    }
    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
