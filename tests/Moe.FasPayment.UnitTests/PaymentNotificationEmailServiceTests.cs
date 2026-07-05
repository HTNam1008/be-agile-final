using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Domain.Payments;
using Moe.Modules.FasPayment.Infrastructure.Persistence;
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
    public async Task SendPaymentSucceededAsync_ForFullPayment_EnqueuesFullPaymentReceipt()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddDirectBillPaymentAsync(dbContext, billCount: 1);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-12-FULL");
        job.PersonId.Should().Be(2001);
        job.Subject.Should().Be("Full Payment Received");
        job.PlainTextBody.Should().Contain("Hello Payment Student");
        job.PlainTextBody.Should().Contain("Amount: SGD 120.00");
        job.PlainTextBody.Should().Contain("Payment Course 101");
        job.HtmlBody.Should().Contain("Full payment received");
    }

    [Fact]
    public async Task SendPaymentSucceededAsync_WithStripeInvoiceUrl_IncludesReceiptLink()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddDirectBillPaymentAsync(dbContext, billCount: 1);
        payment.AttachProviderEvidence(
            "https://stripe.test/invoices/in_123",
            "https://stripe.test/invoices/in_123.pdf",
            "https://stripe.test/receipts/ch_123",
            Now);
        await dbContext.SaveChangesAsync();
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.PlainTextBody.Should().Contain("View MOE Receipt: https://portal.example.test/portal/payments?receiptId=");
        job.PlainTextBody.Should().Contain("Stripe online payment evidence: https://stripe.test/invoices/in_123");
        job.HtmlBody.Should().Contain("View MOE Receipt");
        job.HtmlBody.Should().Contain("View Stripe Evidence");
        job.HtmlBody.Should().Contain("https://stripe.test/invoices/in_123");
    }

    [Fact]
    public async Task SendPaymentSucceededAsync_UsesReceiptUrlWhenInvoiceUrlsMissing()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddDirectBillPaymentAsync(dbContext, billCount: 1);
        payment.AttachProviderEvidence(null, null, "https://stripe.test/receipts/ch_123", Now);
        await dbContext.SaveChangesAsync();
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.PlainTextBody.Should().Contain("Stripe online payment evidence: https://stripe.test/receipts/ch_123");
    }

    [Fact]
    public async Task SendPaymentSucceededAsync_ForInstallmentPayment_EnqueuesInstallmentReceipt()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        Payment payment = await AddDirectBillPaymentAsync(dbContext, billCount: 2);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentSucceededAsync(payment, Now, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-12-INSTALLMENT");
        job.Subject.Should().Be("Installment Payment Received");
        job.PlainTextBody.Should().Contain("installment payment");
        job.PlainTextBody.Should().Contain("Payment Course 101 - installment 1");
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

    [Fact]
    public async Task SendPaymentDeferredAsync_EnqueuesDeferNotice()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        await AddPaymentPersonAsync(dbContext);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentDeferredAsync(
            2001,
            3001,
            [CreateStatementBill(501, 100m, new DateOnly(2026, 7, 15), "Payment Course 101")],
            Now,
            CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-14-DEFERRED");
        job.PersonId.Should().Be(2001);
        job.Subject.Should().Be("Installment Payment Deferred");
        job.PlainTextBody.Should().Contain("Payment Course 101");
        job.PlainTextBody.Should().Contain("SGD 100.00");
        job.PlainTextBody.Should().Contain("15 Jul 2026 -> 15 Aug 2026");
        job.HtmlBody.Should().Contain("Installment payment deferred");
    }

    [Fact]
    public async Task SendPaymentDeferredAsync_ForMultipleBills_IncludesTotalAndBillCount()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new();
        await AddPaymentPersonAsync(dbContext);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        await service.SendPaymentDeferredAsync(
            2001,
            3001,
            [
                CreateStatementBill(501, 100m, new DateOnly(2026, 7, 15), "Payment Course 101"),
                CreateStatementBill(502, 80m, new DateOnly(2026, 7, 20), "Payment Course 102")
            ],
            Now,
            CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.PlainTextBody.Should().Contain("Total Deferred Amount: SGD 180.00");
        job.PlainTextBody.Should().Contain("Deferred Bills: 2");
        job.PlainTextBody.Should().Contain("Payment Course 102");
    }

    [Fact]
    public async Task SendPaymentDeferredAsync_WhenQueueFails_DoesNotThrow()
    {
        using MoeDbContext dbContext = CreateDbContext();
        RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        await AddPaymentPersonAsync(dbContext);
        PaymentNotificationEmailService service = CreateService(dbContext, mailQueue);

        Func<Task> act = () => service.SendPaymentDeferredAsync(
            2001,
            3001,
            [CreateStatementBill(501, 100m, new DateOnly(2026, 7, 15), "Payment Course 101")],
            Now,
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        mailQueue.Jobs.Should().ContainSingle();
    }

    private static async Task<Payment> AddStatementPaymentAsync(MoeDbContext dbContext)
    {
        await AddPaymentPersonAsync(dbContext);

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

    private static async Task AddPaymentPersonAsync(MoeDbContext dbContext)
    {
        if (await dbContext.Set<Person>().AnyAsync(x => x.Id == 2001)) return;
        dbContext.Set<Person>().Add(new Person(
            2001,
            "EXT-PAYMENT-1",
            "Payment Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null));
        await dbContext.SaveChangesAsync();
    }

    private static PayableStatementBill CreateStatementBill(
        long billId,
        decimal outstandingAmount,
        DateOnly currentDueDate,
        string courseName)
        => new(
            BillingStatementItemId: billId + 1000,
            BillId: billId,
            OrganizationId: 2,
            OutstandingAmount: outstandingAmount,
            CurrentDueDate: currentDueDate,
            OriginalDueDate: currentDueDate,
            IsInstallment: true,
            CourseCode: "PAY101",
            CourseName: courseName);

    private static async Task<Payment> AddDirectBillPaymentAsync(MoeDbContext dbContext, int billCount)
    {
        dbContext.Set<Person>().Add(new Person(
            2001,
            "EXT-PAYMENT-1",
            "Payment Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null));
        Course course = new(
            organizationId: 2,
            courseCode: "PAY101",
            courseName: "Payment Course 101",
            description: null,
            startDate: new DateOnly(2026, 8, 12),
            endDate: new DateOnly(2026, 11, 12),
            enrollmentOpenAtUtc: Now.AddDays(-5),
            enrollmentCloseAtUtc: Now.AddDays(5),
            actorLoginAccountId: 99,
            utcNow: Now);
        course.Publish(99, Now);
        dbContext.Set<Course>().Add(course);
        await dbContext.SaveChangesAsync();

        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            2001,
            course.Id,
            coursePaymentPlanId: 10,
            loginAccountId: 1003,
            Now,
            100m,
            50m).Value;
        dbContext.Set<CourseEnrollment>().Add(enrollment);
        await dbContext.SaveChangesAsync();

        Bill[] bills = Enumerable.Range(1, billCount)
            .Select(sequence => Bill.IssueForCourseEnrollment(
                enrollment.Id,
                $"BILL-{Guid.NewGuid():N}"[..30],
                Now,
                DateOnly.FromDateTime(Now).AddMonths(sequence - 1),
                120m,
                sequenceNumber: sequence).Value)
            .ToArray();
        dbContext.Set<Bill>().AddRange(bills);
        await dbContext.SaveChangesAsync();

        Payment payment = Payment.RecordProviderSuccess(
            bills[0].Id,
            2001,
            120m,
            $"pi_{Guid.NewGuid():N}",
            null,
            null,
            installmentNumber: billCount > 1 ? 1 : 0,
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
            new RecordingEmailNotificationScheduler(mailQueue, mailSwitch),
            new FixedEmailBrandingProvider(),
            new PaymentReceiptService(dbContext));

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
            modelBuilder.ApplyConfiguration(new PaymentConfiguration());
            modelBuilder.ApplyConfiguration(new PaymentPartConfiguration());
            modelBuilder.ApplyConfiguration(new PaymentAllocationConfiguration());
            new CourseBillingModelConfiguration().Configure(modelBuilder);
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
