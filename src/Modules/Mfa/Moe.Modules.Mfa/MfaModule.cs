using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.Mfa.Application;
using Moe.Modules.Mfa.Application.ChangePin;
using Moe.Modules.Mfa.Application.GetMfaStatus;
using Moe.Modules.Mfa.Application.ResetPin;
using Moe.Modules.Mfa.Application.SetupPin;
using Moe.Modules.Mfa.Application.StartChallenge;
using Moe.Modules.Mfa.Application.VerifyPin;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.Modules.Mfa.IGateway.Security;
using Moe.Modules.Mfa.Infrastructure;
using Moe.Modules.Mfa.Infrastructure.Repositories;

namespace Moe.Modules.Mfa;

public sealed class MfaModule : IModule
{
    public string Name => "Mfa";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModelConfigurationContributor, MfaModelConfiguration>();
        services.AddOptions<MfaOptions>().BindConfiguration(MfaOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();

        services.AddScoped<IMfaPinHasher, Pbkdf2MfaPinHasher>();
        services.AddScoped<IMfaCredentialRepository, MfaCredentialRepository>();
        services.AddScoped<IMfaChallengeRepository, MfaChallengeRepository>();
        services.AddScoped<IMfaAuditEventRepository, MfaAuditEventRepository>();

        services.AddScoped<IQueryHandler<GetMfaStatusQuery, MfaStatusResponse>, GetMfaStatusHandler>();
        services.AddScoped<ICommandHandler<StartMfaChallengeCommand, MfaChallengeResponse>, StartMfaChallengeHandler>();
        services.AddScoped<ICommandHandler<SetupMfaPinCommand, MfaStatusResponse>, SetupMfaPinHandler>();
        services.AddScoped<ICommandHandler<VerifyMfaPinCommand, MfaVerificationResponse>, VerifyMfaPinHandler>();
        services.AddScoped<ICommandHandler<ChangeMfaPinCommand, MfaStatusResponse>, ChangeMfaPinHandler>();
        services.AddScoped<ICommandHandler<ResetMfaPinCommand, MfaStatusResponse>, ResetMfaPinHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
