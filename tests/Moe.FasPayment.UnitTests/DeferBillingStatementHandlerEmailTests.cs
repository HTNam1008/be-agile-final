using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Application.StatementPayments;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class DeferBillingStatementHandlerEmailTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 4, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenDeferSucceeds_EnqueuesDeferEmail()
    {
        TestContext context = CreateContext();
        DeferBillingStatementHandler handler = context.CreateHandler();

        Result<DeferBillingStatementResponse> result = await handler.Handle(
            new DeferBillingStatementCommand(3001, new DeferBillingStatementRequest([501])),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Deferred.Should().BeTrue();
        context.CoursePayments.DeferCalls.Should().Be(1);
        EmailNotificationJob job = context.MailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-14-DEFERRED");
        job.PlainTextBody.Should().Contain("Payment Course 101");
    }

    [Fact]
    public async Task Handle_WhenEducationAccountCanCoverDeferral_DoesNotEnqueueEmail()
    {
        TestContext context = CreateContext(availableBalance: 200m);
        DeferBillingStatementHandler handler = context.CreateHandler();

        Result<DeferBillingStatementResponse> result = await handler.Handle(
            new DeferBillingStatementCommand(3001, new DeferBillingStatementRequest([501])),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Deferred.Should().BeFalse();
        context.CoursePayments.DeferCalls.Should().Be(0);
        context.MailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenDeferGatewayFails_DoesNotEnqueueEmail()
    {
        TestContext context = CreateContext(deferResult: Result.Failure(new Error("DEFER.FAIL", "Failed.")));
        DeferBillingStatementHandler handler = context.CreateHandler();

        Result<DeferBillingStatementResponse> result = await handler.Handle(
            new DeferBillingStatementCommand(3001, new DeferBillingStatementRequest([501])),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        context.MailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenDeferEmailQueueFails_StillReturnsDeferred()
    {
        TestContext context = CreateContext();
        context.MailQueue.Result = Result.Failure(new Error("MAIL.FAIL", "Mail failed."));
        DeferBillingStatementHandler handler = context.CreateHandler();

        Result<DeferBillingStatementResponse> result = await handler.Handle(
            new DeferBillingStatementCommand(3001, new DeferBillingStatementRequest([501])),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Deferred.Should().BeTrue();
        context.MailQueue.Jobs.Should().ContainSingle();
    }

    private static TestContext CreateContext(
        decimal availableBalance = 0m,
        Result? deferResult = null)
    {
        MoeDbContext dbContext = CreateDbContext();
        dbContext.Set<Person>().Add(new Person(
            2001,
            "EXT-DEFER-1",
            "Defer Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null));
        dbContext.SaveChanges();

        RecordingEmailNotificationQueue mailQueue = new();
        PaymentNotificationEmailService notifications = new(
            dbContext,
            new RecordingEmailNotificationScheduler(mailQueue, new FixedEmailDeliverySwitch()),
            new FixedEmailBrandingProvider(),
            new PaymentReceiptService(dbContext));

        return new TestContext(
            dbContext,
            mailQueue,
            new CoursePaymentGatewayDouble(deferResult ?? Result.Success()),
            new EducationAccountGatewayDouble(availableBalance),
            notifications);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed record TestContext(
        MoeDbContext DbContext,
        RecordingEmailNotificationQueue MailQueue,
        CoursePaymentGatewayDouble CoursePayments,
        EducationAccountGatewayDouble Accounts,
        PaymentNotificationEmailService Notifications)
    {
        public DeferBillingStatementHandler CreateHandler()
            => new(
                CoursePayments,
                Accounts,
                new CurrentUserDouble(),
                new FixedClock(Now),
                Notifications);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
        }
    }

    private sealed class CoursePaymentGatewayDouble(Result deferResult) : ICoursePaymentGateway
    {
        public int DeferCalls { get; private set; }

        public Task<PayableCourseBill?> FindPayableBillAsync(long billId, long personId, CancellationToken cancellationToken)
            => Task.FromResult<PayableCourseBill?>(null);

        public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);

        public Task ApplySuccessfulPaymentAsync(long billId, decimal amount, bool paidInFull, DateTime paidAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendInstallmentEnrollmentConfirmationAsync(long courseEnrollmentId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyPaymentFailureAsync(long billId, string failureReason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyFullRefundForBillsAsync(IReadOnlyCollection<long> billIds, DateTime refundedAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PayableStatement?> FindPayableStatementAsync(long statementId, long personId, CancellationToken cancellationToken)
            => Task.FromResult<PayableStatement?>(new(
                statementId,
                personId,
                100m,
                "SGD",
                [CreateStatementBill(501, 100m, new DateOnly(2026, 7, 15), "Payment Course 101")]));

        public Task ApplyStatementPaymentAsync(
            long statementId,
            IReadOnlyCollection<BillPaymentAllocation> allocations,
            DateTime paidAtUtc,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<IReadOnlyCollection<PaymentCheckoutLineItem>> BuildPaymentCheckoutLineItemsAsync(
            IReadOnlyCollection<long> billIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<PaymentCheckoutLineItem>>([]);

        public Task<Result> DeferStatementAsync(
            long statementId,
            long personId,
            IReadOnlyCollection<long> billIds,
            long actorLoginAccountId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            DeferCalls++;
            return Task.FromResult(deferResult);
        }
    }

    private sealed class EducationAccountGatewayDouble(decimal availableBalance) : IEducationAccountPaymentGateway
    {
        public Task<EducationAccountPaymentBalance?> GetAvailableBalanceAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<EducationAccountPaymentBalance?>(new(1, availableBalance, 0m, availableBalance, "SGD"));

        public Task<long> ReserveAsync(long personId, long paymentPartId, decimal amount, DateTime expiresAtUtc, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task<long> CaptureAsync(long accountHoldId, long? actorLoginAccountId, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task ReleaseAsync(long accountHoldId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<long> DebitImmediatelyAsync(long personId, long paymentPartId, decimal amount, long? actorLoginAccountId, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task<long> CreditRefundAsync(
            long personId,
            long refundReferenceId,
            decimal amount,
            long? reversalOfTransactionId,
            string idempotencyKey,
            long? actorLoginAccountId,
            CancellationToken cancellationToken)
            => Task.FromResult(1L);
    }

    private sealed class CurrentUserDouble : ICurrentUser
    {
        public long? UserAccountId => 1003;
        public long? PersonId => 2001;
        public long? OrganizationUnitId => 2;
        public IReadOnlyCollection<long> OrganizationUnitIds => [2];
        public IReadOnlyCollection<string> Roles => ["STUDENT"];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "ESERVICE";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => false;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(utcNow, TimeSpan.Zero);

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled = true) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }

    private sealed class RecordingEmailNotificationQueue : IEmailNotificationQueue
    {
        public List<EmailNotificationJob> Jobs { get; } = [];
        public Result Result { get; set; } = Result.Success();

        public ValueTask<Result> EnqueueAsync(EmailNotificationJob job, CancellationToken cancellationToken)
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
}
