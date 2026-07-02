using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.MailDelivery;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.MailDelivery.Infrastructure.Queue;
using Moe.Modules.MailDelivery.Infrastructure.Smtp;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
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
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        await AddNotificationAsync(provider, personId: 42);
        QueuedEmailDeliveryWorker worker = new(
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
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
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(
            expectedMessageCount: 2,
            Result.Failure(MailDeliveryErrors.SendFailed("First send failed.")),
            Result.Success());
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        await AddNotificationAsync(provider, personId: 51);
        await AddNotificationAsync(provider, personId: 52);
        QueuedEmailDeliveryWorker worker = new(
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await gateway.WaitForExpectedMessagesAsync();
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        gateway.Messages.Should().HaveCount(2);
        resolver.PersonIds.Should().Equal(51, 52);

        List<EmailNotification> notifications = await GetNotificationsAsync(provider);
        notifications.Should().ContainSingle(x =>
            x.PersonId == 51
            && x.StatusCode == EmailNotificationStatusCodes.FailedRetryable
            && x.AttemptCount == 1
            && x.NextAttemptAtUtc > new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        notifications.Should().ContainSingle(x =>
            x.PersonId == 52
            && x.StatusCode == EmailNotificationStatusCodes.Sent);
    }

    [Fact]
    public async Task Worker_WhenMailDeliveryIsDisabled_DoesNotResolveOrSend()
    {
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        await AddNotificationAsync(provider, personId: 61);
        QueuedEmailDeliveryWorker worker = new(
            new FixedEmailDeliverySwitch(false),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        resolver.PersonIds.Should().BeEmpty();
        gateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Worker_WhenProcessingLockExpired_ReclaimsAndSends()
    {
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        await AddNotificationAsync(
            provider,
            personId: 71,
            beforeSave: notification => notification.MarkProcessing(
                new DateTime(2026, 6, 30, 23, 59, 0, DateTimeKind.Utc)));
        QueuedEmailDeliveryWorker worker = new(
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(CreateOptions()),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await gateway.WaitForExpectedMessagesAsync();
        await worker.StopAsync(CancellationToken.None);

        gateway.Messages.Should().ContainSingle();
        (await GetNotificationsAsync(provider)).Single().StatusCode.Should().Be(EmailNotificationStatusCodes.Sent);
    }

    [Fact]
    public async Task Worker_WhenRateLimitReached_LeavesRemainingJobsPending()
    {
        RecordingRecipientResolver resolver = new("student.contact@example.com");
        RecordingEmailGateway gateway = new(expectedMessageCount: 1);
        using ServiceProvider provider = CreateProvider(resolver, gateway);
        await AddNotificationAsync(provider, personId: 81);
        await AddNotificationAsync(provider, personId: 82);
        MailDeliveryOptions options = CreateOptions(maxEmailsPerMinute: 1);
        QueuedEmailDeliveryWorker worker = new(
            new FixedEmailDeliverySwitch(true),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<QueuedEmailDeliveryWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await gateway.WaitForExpectedMessagesAsync();
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);

        gateway.Messages.Should().ContainSingle();
        List<EmailNotification> notifications = await GetNotificationsAsync(provider);
        notifications.Count(x => x.StatusCode == EmailNotificationStatusCodes.Sent).Should().Be(1);
        notifications.Count(x => x.StatusCode == EmailNotificationStatusCodes.Pending).Should().Be(1);
    }

    private static ServiceProvider CreateProvider(
        IEmailRecipientResolver resolver,
        IEmailDeliveryGateway gateway)
    {
        ServiceCollection services = new();
        string databaseName = Guid.NewGuid().ToString();
        services.AddScoped(_ => resolver);
        services.AddSingleton(gateway);
        services.AddSingleton<IClock>(new TestClock(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<IModelConfigurationContributor, MailDeliveryModelConfiguration>();
        services.AddDbContext<MoeDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static async Task AddNotificationAsync(
        ServiceProvider provider,
        long personId,
        Action<EmailNotification>? beforeSave = null)
    {
        using IServiceScope scope = provider.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        EmailNotification notification = EmailNotification.Create(
            "NOTI-TEST",
            personId,
            "Test subject",
            "Test body",
            "<p>Test body</p>",
            "TestEntity",
            personId.ToString(),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            maxAttempts: 3);
        beforeSave?.Invoke(notification);
        dbContext.Set<EmailNotification>().Add(notification);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<List<EmailNotification>> GetNotificationsAsync(ServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        MoeDbContext dbContext = scope.ServiceProvider.GetRequiredService<MoeDbContext>();
        return await dbContext.Set<EmailNotification>()
            .OrderBy(x => x.PersonId)
            .ToListAsync();
    }

    private static MailDeliveryOptions CreateOptions(int maxEmailsPerMinute = 60)
        => new()
        {
            Enabled = true,
            AppName = MailDeliveryOptions.DefaultAppName,
            PortalBaseUrl = MailDeliveryOptions.DefaultPortalBaseUrl,
            Host = "smtp.test.local",
            UserName = "sender@example.com",
            Password = "password",
            FromEmail = "sender@example.com",
            FromDisplayName = MailDeliveryOptions.DefaultAppName,
            Worker = new MailDeliveryWorkerOptions
            {
                BatchSize = 10,
                PollIntervalSeconds = 1,
                MaxAttempts = 3,
                MaxEmailsPerMinute = maxEmailsPerMinute,
                LockSeconds = 30
            }
        };

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

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
