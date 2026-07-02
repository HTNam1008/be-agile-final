using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Modules;
using Moe.Modules.MailDelivery.Application.Admin;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Queue;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;

namespace Moe.Modules.MailDelivery;

public sealed class MailDeliveryModule : IModule
{
    public string Name => "MailDelivery";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MailDeliveryOptions>()
            .BindConfiguration(MailDeliveryOptions.SectionName)
            .Validate(MailDeliveryOptions.IsValid, "MailDelivery configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IModelConfigurationContributor, MailDeliveryModelConfiguration>();
        services.AddSingleton<IEmailDeliverySwitch, EmailDeliverySwitch>();
        services.AddSingleton<IEmailBrandingProvider, EmailBrandingProvider>();
        services.AddSingleton<IEmailDeliveryGateway, SmtpEmailDeliveryGateway>();
        services.AddSingleton<IEmailNotificationQueue, InMemoryEmailNotificationQueue>();
        services.AddScoped<IEmailNotificationScheduler, EmailNotificationScheduler>();
        services.TryAddScoped<IMailNotificationAccessScope, AllowAllMailNotificationAccessScope>();
        services.AddScoped<IMailNotificationAdminService, MailNotificationAdminService>();
        if (IsBackgroundJobEnabled(configuration, "MailDelivery:QueueWorker"))
        {
            services.AddHostedService<QueuedEmailDeliveryWorker>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }

    private static bool IsBackgroundJobEnabled(IConfiguration configuration, string key)
        => configuration.GetValue("BackgroundJobs:Enabled", true)
           && configuration.GetValue($"BackgroundJobs:{key}", true);
}
