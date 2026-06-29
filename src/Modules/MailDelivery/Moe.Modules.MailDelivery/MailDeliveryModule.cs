using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Modules;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;

namespace Moe.Modules.MailDelivery;

public sealed class MailDeliveryModule : IModule
{
    public string Name => "MailDelivery";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MailDeliveryOptions>()
            .BindConfiguration(MailDeliveryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IEmailDeliveryGateway, SmtpEmailDeliveryGateway>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) { }
}
