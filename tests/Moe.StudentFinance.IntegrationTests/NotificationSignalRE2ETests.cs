using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Moe.Modules.Notifications.Api.Notifications;
using Moe.Modules.Notifications.Application;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class NotificationSignalRE2ETests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const long TestAdminUserAccountId = 1001;

    [Fact]
    public async Task CreateNotification_Pushes_RealtimeSignalRMessage_ToConnectedRecipient()
    {
        using HttpClient client = factory.CreateClient();
        await using HubConnection connection = CreateAdminNotificationConnection();
        TaskCompletionSource<NotificationRealtimeMessage> received = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<NotificationRealtimeMessage>(
            NotificationHub.NotificationReceivedMethodName,
            message => received.TrySetResult(message));

        await connection.StartAsync();

        const string title = "SignalR e2e notification";
        const string body = "Notification realtime delivery is working.";
        using HttpResponseMessage response = await SendCreateNotificationAsync(
            client,
            new NotificationCreateRequest(
                TestAdminUserAccountId,
                NotificationTypeCode.AccOpened,
                title,
                body));

        response.EnsureSuccessStatusCode();
        NotificationRealtimeMessage message = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(message.NotificationId > 0);
        Assert.Equal(NotificationTypeCode.AccOpened, message.NotificationTypeCode);
        Assert.Equal(title, message.Title);
        Assert.Equal(body, message.Body);
    }

    private HubConnection CreateAdminNotificationConnection()
        => new HubConnectionBuilder()
            .WithUrl(
                "http://localhost/hubs/notifications",
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
            .Build();

    private static async Task<HttpResponseMessage> SendCreateNotificationAsync(
        HttpClient client,
        NotificationCreateRequest requestBody)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/admin/v1/me/notifications")
        {
            Content = JsonContent.Create(requestBody)
        };

        return await client.SendAsync(request);
    }
}
