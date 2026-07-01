using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class PaymentNotificationEmailServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 4, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendPaymentSucceededAsync_EnqueuesPaymentReceipt()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddStatementPaymentAsync(dbContext);
        payment.MarkSuccessful(Now);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-12");
        job.PersonId.Should().Be(2001);
        job.Subject.Should().Be("Payment Received");
        job.PlainTextBody.Should().Contain("Hello Payment Student");
        job.PlainTextBody.Should().Contain("Amount: SGD 120.00");
        job.PlainTextBody.Should().Contain("Monthly billing statement 3001");
        job.HtmlBody.Should().Contain("Payment received");
    }

    [Fact]
    public async Task SendPaymentCancelledAsync_EnqueuesCancelledNotice()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddStatementPaymentAsync(dbContext);
        payment.MarkCancelled(Now);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentCancelledAsync(
            payment,
            Now,
            releasedEducationAccountHold: true,
            CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-13");
        job.Subject.Should().Be("Payment Cancelled");
        job.PlainTextBody.Should().Contain("payment attempt was cancelled");
        job.PlainTextBody.Should().Contain("reserved Education Account funds have been released");
    }

    [Fact]
    public async Task SendPaymentExpiredAsync_UsesPaymentFailedNotificationWithExpiredReason()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddStatementPaymentAsync(dbContext);
        payment.MarkExpired(Now);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentExpiredAsync(payment, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-09");
        job.Subject.Should().Be("Action Required: Your Payment Failed");
        job.PlainTextBody.Should().Contain("The payment session expired before completion. Please try again.");
    }

    [Fact]
    public async Task SendPaymentSucceededAsync_WhenMailDeliveryDisabled_DoesNotEnqueue()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddStatementPaymentAsync(dbContext);
        PaymentNotificationEmailService service = CreateService(
            dbContext,
            mailQueue,
            new FixedEmailDeliverySwitch(isEnabled: false));

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendPaymentSucceededAsync_WhenQueueFails_DoesNotThrow()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        Payment payment = await AddStatementPaymentAsync(dbContext);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        Func<Task> act = () => service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        await act.Should().NotThrowAsync();
        mailQueue.Jobs.Should().ContainSingle();
    }

    private static async Task<Payment> AddStatementPaymentAsync(MoeDbContext dbContext)
    {
        dbContext.Set<Person>().Add(new Person(
            2001,
            "EXT-PAYMENT-1",
            "Payment Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null));

        Payment payment = Payment.StartStatementPayment(
            3001,
            2001,
            120m,
            20m,
            100m,
            "payment-email-test",
            Now);

        dbContext.Set<Payment>().Add(payment);
        await dbContext.SaveChangesAsync();
        return payment;
    }

    private static PaymentNotificationEmailService CreateService(
        MoeDbContext dbContext,
        IEmailNotificationQueue mailQueue,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            mailQueue,
            mailSwitch ?? new FixedEmailDeliverySwitch(),
            NullLogger<PaymentNotificationEmailService>.Instance);

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<Payment>().HasKey(x => x.Id);
        }
    }

    private sealed class RecordingEmailNotificationQueue : IEmailNotificationQueue
    {
        public List<EmailNotificationJob> Jobs { get; } = [];
        public Result Result { get; init; } = Result.Success();

        public ValueTask<Result> EnqueueAsync(
            EmailNotificationJob job,
            CancellationToken cancellationToken)
        {
            Jobs.Add(job);
            return ValueTask.FromResult(Result);
        }

        public async IAsyncEnumerable<EmailNotificationJob> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (EmailNotificationJob job in Jobs)
            {
                yield return job;
            }
        }
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled = true) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }
}
