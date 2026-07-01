using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Modules;
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

        services.AddSingleton<IEmailDeliverySwitch, EmailDeliverySwitch>();
        services.AddSingleton<IEmailDeliveryGateway, SmtpEmailDeliveryGateway>();
        services.AddSingleton<IEmailNotificationQueue, InMemoryEmailNotificationQueue>();
        services.AddHostedService<QueuedEmailDeliveryWorker>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
