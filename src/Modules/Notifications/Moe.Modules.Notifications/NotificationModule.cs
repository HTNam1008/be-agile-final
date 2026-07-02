using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Modules;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.Notifications.Api.Notifications;
using Moe.Modules.Notifications.Application.GetMyNotifications;
using Moe.Modules.Notifications.Application.GetUnreadNotificationCount;
using Moe.Modules.Notifications.Application.MarkNotificationAsRead;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.Modules.Notifications.Infrastructure.Notifications;

namespace Moe.Modules.Notifications;

public sealed class NotificationModule : IModule
{
    public string Name => "Notifications";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NotificationRealtimeOptions>()
            .BindConfiguration(NotificationRealtimeOptions.SectionName)
            .Validate(NotificationRealtimeOptions.IsValid, "Notifications realtime configuration is invalid.")
            .ValidateOnStart();

        services.AddSingleton<IModelConfigurationContributor, NotificationModelConfiguration>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationRealtimeNotifier, SignalRNotificationRealtimeNotifier>();
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<IQueryHandler<GetMyNotificationsQuery, Moe.Infrastructure.Shared.Api.PageResponse<MyNotificationItem>>, GetMyNotificationsHandler>();
        services.AddScoped<IQueryHandler<GetUnreadNotificationCountQuery, long>, GetUnreadNotificationCountHandler>();
        services.AddScoped<ICommandHandler<MarkNotificationAsReadCommand>, MarkNotificationAsReadHandler>();

        if (IsBackgroundJobEnabled(configuration, "Notifications:RealtimeWorker"))
        {
            services.AddHostedService<QueuedNotificationRealtimeDeliveryWorker>();
        }
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<NotificationHub>("/hubs/notifications")
            .RequireCors("PortalCors");
    }

    private static bool IsBackgroundJobEnabled(IConfiguration configuration, string key)
        => configuration.GetValue("BackgroundJobs:Enabled", true)
           && configuration.GetValue($"BackgroundJobs:{key}", true);
}
