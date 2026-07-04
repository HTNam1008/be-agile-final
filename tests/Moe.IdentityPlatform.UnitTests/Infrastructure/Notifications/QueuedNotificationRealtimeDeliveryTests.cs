using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.Notifications;
using Moe.Modules.Notifications.Application;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.Modules.Notifications.Infrastructure.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.Notifications;

public sealed class QueuedNotificationRealtimeDeliveryTests
{
    [Fact]
    public async Task Writer_CreatesNotificationAndRealtimeDelivery_WhenRealtimeIsEnabled()
    {
        using ServiceProvider provider = CreateProvider(new RecordingRealtimeNotifier(expectedMessageCount: 0));
        using IServiceScope scope = provider.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        NotificationWriter writer = new(
            dbContext,
            Options.Create(CreateOptions()));

        Result<long> result = await writer.CreateAsync(CreateRequest());

        result.IsSuccess.Should().BeTrue();
        (await dbContext.Set<Notification>().CountAsync()).Should().Be(1);
        NotificationRealtimeDelivery delivery = await dbContext.Set<NotificationRealtimeDelivery>()
            .SingleAsync();
        delivery.NotificationId.Should().Be(result.Value);
        delivery.RecipientUserAccountId.Should().Be(1001);
        delivery.StatusCode.Should().Be(NotificationRealtimeDeliveryStatusCodes.Pending);
    }

    [Fact]
    public async Task Worker_DeliversPendingRealtimeNotification()
    {
        RecordingRealtimeNotifier notifier = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(notifier);
        await AddNotificationAsync(provider);
        QueuedNotificationRealtimeDeliveryWorker worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        await notifier.WaitForExpectedMessagesAsync();
        await WaitForDeliveryStatusAsync(provider, NotificationRealtimeDeliveryStatusCodes.Delivered);
        await worker.StopAsync(CancellationToken.None);

        notifier.Messages.Should().ContainSingle();
        notifier.Messages.Single().UserAccountId.Should().Be(1001);
        notifier.Messages.Single().Message.Title.Should().Be("Test notification");

        NotificationRealtimeDelivery delivery = await GetSingleDeliveryAsync(provider);
        delivery.StatusCode.Should().Be(NotificationRealtimeDeliveryStatusCodes.Delivered);
        delivery.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Worker_WhenRealtimeSendThrows_RecordsRetryableFailure()
    {
        RecordingRealtimeNotifier notifier = new(
            expectedMessageCount: 1,
            new InvalidOperationException("SignalR unavailable."));
        using ServiceProvider provider = CreateProvider(notifier);
        await AddNotificationAsync(provider);
        QueuedNotificationRealtimeDeliveryWorker worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        await notifier.WaitForExpectedMessagesAsync();
        await WaitForDeliveryStatusAsync(provider, NotificationRealtimeDeliveryStatusCodes.FailedRetryable);
        await worker.StopAsync(CancellationToken.None);

        NotificationRealtimeDelivery delivery = await GetSingleDeliveryAsync(provider);
        delivery.StatusCode.Should().Be(NotificationRealtimeDeliveryStatusCodes.FailedRetryable);
        delivery.AttemptCount.Should().Be(1);
        delivery.LastErrorCode.Should().Be("NOTIFICATIONS.REALTIME_SEND_FAILED");
        delivery.LastErrorMessage.Should().Contain("SignalR unavailable.");
    }

    [Fact]
    public async Task Worker_WhenProcessingLockExpired_ReclaimsAndDelivers()
    {
        RecordingRealtimeNotifier notifier = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(notifier);
        await AddNotificationAsync(
            provider,
            delivery => delivery.MarkProcessing(new DateTime(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc)));
        QueuedNotificationRealtimeDeliveryWorker worker = CreateWorker(provider);

        await worker.StartAsync(CancellationToken.None);
        await notifier.WaitForExpectedMessagesAsync();
        await WaitForDeliveryStatusAsync(provider, NotificationRealtimeDeliveryStatusCodes.Delivered);
        await worker.StopAsync(CancellationToken.None);

        NotificationRealtimeDelivery delivery = await GetSingleDeliveryAsync(provider);
        delivery.StatusCode.Should().Be(NotificationRealtimeDeliveryStatusCodes.Delivered);
    }

    private static QueuedNotificationRealtimeDeliveryWorker CreateWorker(ServiceProvider provider)
        => new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<QueuedNotificationRealtimeDeliveryWorker>.Instance);

    private static ServiceProvider CreateProvider(RecordingRealtimeNotifier notifier)
    {
        ServiceCollection services = new();
        string databaseName = Guid.NewGuid().ToString();
        services.AddSingleton<IClock>(new TestClock(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<IModelConfigurationContributor, NotificationModelConfiguration>();
        services.AddSingleton<INotificationRealtimeNotifier>(notifier);
        services.AddDbContext<MoeDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static async Task AddNotificationAsync(
        ServiceProvider provider,
        Action<NotificationRealtimeDelivery>? beforeSave = null)
    {
        using IServiceScope scope = provider.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        Notification notification = new(
            1001,
            NotificationTypeCode.AccOpened,
            NotificationSourceEpicCode.Account,
            NotificationChannelCode.InApp,
            NotificationTypeCode.AccOpened,
            "Test notification",
            "Test body",
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        NotificationRealtimeDelivery delivery = NotificationRealtimeDelivery.Create(
            notification,
            1001,
            notification.CreatedAtUtc,
            maxAttempts: 3);

        beforeSave?.Invoke(delivery);

        dbContext.Set<Notification>().Add(notification);
        dbContext.Set<NotificationRealtimeDelivery>().Add(delivery);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<NotificationRealtimeDelivery> GetSingleDeliveryAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        return await dbContext.Set<NotificationRealtimeDelivery>().SingleAsync();
    }

    private static async Task WaitForDeliveryStatusAsync(ServiceProvider provider, string expectedStatusCode)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!timeout.IsCancellationRequested)
        {
            NotificationRealtimeDelivery delivery = await GetSingleDeliveryAsync(provider);
            if (delivery.StatusCode == expectedStatusCode)
            {
                return;
            }

            await Task.Delay(50, timeout.Token);
        }

        NotificationRealtimeDelivery finalDelivery = await GetSingleDeliveryAsync(provider);
        finalDelivery.StatusCode.Should().Be(expectedStatusCode);
    }

    private static NotificationCreateRequest CreateRequest()
        => new(
            1001,
            NotificationTypeCode.AccOpened,
            "Test notification",
            "Test body");

    private static NotificationRealtimeOptions CreateOptions()
        => new()
        {
            Enabled = true,
            Worker = new NotificationRealtimeWorkerOptions
            {
                Enabled = true,
                BatchSize = 10,
                PollIntervalSeconds = 1,
                LockSeconds = 30,
                MaxAttempts = 3
            }
        };

    private sealed class RecordingRealtimeNotifier(
        int expectedMessageCount,
        Exception? exception = null) : INotificationRealtimeNotifier
    {
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(long UserAccountId, NotificationRealtimeMessage Message)> Messages { get; } = [];

        public Task NotifyUserAccountAsync(
            long userAccountId,
            NotificationRealtimeMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add((userAccountId, message));
            if (Messages.Count >= expectedMessageCount)
            {
                _completion.TrySetResult(true);
            }

            if (exception is not null)
            {
                throw exception;
            }

            return Task.CompletedTask;
        }

        public Task WaitForExpectedMessagesAsync()
            => expectedMessageCount == 0
                ? Task.CompletedTask
                : _completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
