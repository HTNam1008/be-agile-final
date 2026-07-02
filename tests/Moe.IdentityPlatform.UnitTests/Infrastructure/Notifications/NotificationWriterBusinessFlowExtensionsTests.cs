using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.Notifications;

public sealed class NotificationWriterBusinessFlowExtensionsTests
{
    [Fact]
    public async Task CreateForBusinessFlowAsync_WhenWriterReturnsFailure_ReturnsFalse()
    {
        var writer = new StubNotificationWriter(
            _ => Task.FromResult(Result<long>.Failure(new Error("notification.failed", "Notification failed."))));

        bool created = await writer.CreateForBusinessFlowAsync(
            CreateRequest(),
            NullLogger.Instance,
            "test business flow",
            CancellationToken.None);

        created.Should().BeFalse();
    }

    [Fact]
    public async Task CreateForBusinessFlowAsync_WhenWriterThrows_ReturnsFalse()
    {
        var writer = new StubNotificationWriter(_ => throw new InvalidOperationException("writer unavailable"));

        bool created = await writer.CreateForBusinessFlowAsync(
            CreateRequest(),
            NullLogger.Instance,
            "test business flow",
            CancellationToken.None);

        created.Should().BeFalse();
    }

    [Fact]
    public async Task CreateForBusinessFlowAsync_WhenCancellationIsRequested_RethrowsCancellation()
    {
        var writer = new StubNotificationWriter(_ => throw new OperationCanceledException());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => writer.CreateForBusinessFlowAsync(
            CreateRequest(),
            NullLogger.Instance,
            "test business flow",
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static NotificationCreateRequest CreateRequest()
        => new(
            123,
            NotificationTypeCode.AccOpened,
            "Account opened",
            "Your account has been opened.");

    private sealed class StubNotificationWriter(
        Func<NotificationCreateRequest, Task<Result<long>>> create) : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(
            NotificationCreateRequest request,
            CancellationToken cancellationToken = default)
            => create(request);
    }
}
