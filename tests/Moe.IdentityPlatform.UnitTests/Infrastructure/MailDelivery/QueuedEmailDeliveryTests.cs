using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Queue;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.MailDelivery;

public sealed class QueuedEmailDeliveryTests
{
    [Fact]
    public async Task EnqueueAsync_WhenQueueIsFull_ReturnsFailure()
    {
        InMemoryEmailNotificationQueue queue = new();

        for (int index = 0; index < 1_000; index++)
        {
            Result result = await queue.EnqueueAsync(CreatePersonJob(index + 1), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }

        Result overflow = await queue.EnqueueAsync(CreatePersonJob(1_001), CancellationToken.None);

        overflow.IsFailure.Should().BeTrue();
        overflow.Error.Code.Should().Be("MAIL_DELIVERY.QUEUE_FULL");
    }

    [Fact]
    public async Task Worker_ResolvesPersonRecipient_AndSendsEmail()
    {
        InMemoryEmailNotificationQueue queue = new();
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        QueuedEmailDeliveryWorker worker = new(
            queue,
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(CreatePersonJob(42), CancellationToken.None);
        await gateway.WaitForExpectedMessagesAsync();
        await worker.StopAsync(CancellationToken.None);

        resolver.PersonIds.Should().Equal(42);
        gateway.Messages.Should().ContainSingle();
        gateway.Messages.Single().ToEmail.Should().Be("student.contact@example.com");
        gateway.Messages.Single().Subject.Should().Be("Test subject");
    }

    [Fact]
    public async Task Worker_WhenGatewayFails_ContinuesWithNextJob()
    {
        InMemoryEmailNotificationQueue queue = new();
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(
            expectedMessageCount: 2,
            Result.Failure(new Error("MAIL.TEST_FAILURE", "First send failed.")),
            Result.Success());
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        QueuedEmailDeliveryWorker worker = new(
            queue,
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(CreatePersonJob(51), CancellationToken.None);
        await queue.EnqueueAsync(CreatePersonJob(52), CancellationToken.None);
        await gateway.WaitForExpectedMessagesAsync();
        await worker.StopAsync(CancellationToken.None);

        gateway.Messages.Should().HaveCount(2);
        resolver.PersonIds.Should().Equal(51, 52);
    }

    [Fact]
    public async Task Worker_WhenMailDeliveryIsDisabled_DoesNotResolveOrSend()
    {
        InMemoryEmailNotificationQueue queue = new();
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        QueuedEmailDeliveryWorker worker = new(
            queue,
            new FixedEmailDeliverySwitch(false),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await queue.EnqueueAsync(CreatePersonJob(61), CancellationToken.None);
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        resolver.PersonIds.Should().BeEmpty();
        gateway.Messages.Should().BeEmpty();
    }

    private static ServiceProvider CreateProvider(
        IEmailRecipientResolver resolver,
        IEmailDeliveryGateway gateway)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => resolver);
        services.AddSingleton(gateway);
        return services.BuildServiceProvider();
    }

    private static EmailNotificationJob CreatePersonJob(long personId)
        => EmailNotificationJob.ForPerson(
            "NOTI-TEST",
            personId,
            "Test subject",
            "Test body",
            "<p>Test body</p>",
            "TestEntity",
            personId.ToString());

    private sealed class RecordingRecipientResolver(string emailAddress) : IEmailRecipientResolver
    {
        public List<long> PersonIds { get; } = [];

        public Task<EmailRecipient?> ResolveForPersonAsync(
            long personId,
            CancellationToken cancellationToken)
        {
            PersonIds.Add(personId);
            return Task.FromResult<EmailRecipient?>(
                new EmailRecipient(emailAddress, EmailRecipientSourceCodes.Contact));
        }

    }

    private sealed class RecordingEmailGateway : IEmailDeliveryGateway
    {
        private readonly int _expectedMessageCount;
        private readonly Queue<Result> _results;
        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RecordingEmailGateway(int expectedMessageCount, params Result[] results)
        {
            _expectedMessageCount = expectedMessageCount;
            _results = new Queue<Result>(results);
        }

        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Task<Result> SendAsync(
            EmailDeliveryMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            if (Messages.Count >= _expectedMessageCount)
            {
                _completion.TrySetResult(true);
            }

            Result result = _results.Count > 0 ? _results.Dequeue() : Result.Success();
            return Task.FromResult(result);
        }

        public Task WaitForExpectedMessagesAsync()
            => _completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }
}
