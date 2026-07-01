using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.CourseBilling.UnitTests.TestDoubles;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Payments;

public sealed class MissedInstallmentPaymentEmailWorkerTests
{
    private static readonly DateTimeOffset TodayUtc = new(2026, 7, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendDueNotificationsAsync_EligibleInstallment_EnqueuesNotification()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 6, 30),
            outstandingAmount: 25m,
            planTypeCode: "INSTALLMENT");

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        EmailNotificationJob job = context.Queue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-11");
        job.PersonId.Should().Be(801);
        job.Subject.Should().Be("Missed Installment Payment");
        job.PlainTextBody.Should().Contain("SGD 25.00").And.Contain("30 Jun 2026");
    }

    [Fact]
    public async Task SendDueNotificationsAsync_SameBillRepeatedInProcess_EnqueuesOnce()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 6, 30),
            outstandingAmount: 25m,
            planTypeCode: "INSTALLMENT");

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);
        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        context.Queue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task SendDueNotificationsAsync_FullPaymentPlan_DoesNotEnqueue()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 6, 30),
            outstandingAmount: 25m,
            planTypeCode: "FULL_PAYMENT");

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        context.Queue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueNotificationsAsync_BillNotDueYesterday_DoesNotEnqueue()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 7, 1),
            outstandingAmount: 25m,
            planTypeCode: "INSTALLMENT");

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        context.Queue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueNotificationsAsync_PaidBill_DoesNotEnqueue()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 6, 30),
            outstandingAmount: 0m,
            planTypeCode: "INSTALLMENT");

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        context.Queue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueNotificationsAsync_MailDisabled_DoesNotResolvePlanOrEnqueue()
    {
        TestContext context = await CreateContextAsync(
            dueDate: new DateOnly(2026, 6, 30),
            outstandingAmount: 25m,
            planTypeCode: "INSTALLMENT",
            mailEnabled: false);

        await context.Worker.SendDueNotificationsAsync(CancellationToken.None);

        context.PaymentPlans.Calls.Should().Be(0);
        context.Queue.Jobs.Should().BeEmpty();
    }

    private static async Task<TestContext> CreateContextAsync(
        DateOnly dueDate,
        decimal outstandingAmount,
        string planTypeCode,
        bool mailEnabled = true)
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"missed-installment-worker-{Guid.NewGuid():N}")
            .Options;
        IModelConfigurationContributor[] contributors =
        [
            new CourseBillingModelConfiguration(),
            new PersonOnlyModelConfiguration()
        ];

        await using (MoeDbContext dbContext = new(options, contributors))
        {
            await dbContext.Database.EnsureCreatedAsync();
            Person person = new(801, "PERSON-801", "Installment Student", new DateOnly(2008, 1, 1), "SG", "CITIZEN");
            Course course = new(
                10,
                "INSTALLMENT-TEST",
                "Installment Test Course",
                null,
                new DateOnly(2026, 7, 10),
                new DateOnly(2026, 8, 10),
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                1,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
            dbContext.AddRange(person, course);
            await dbContext.SaveChangesAsync();

            CourseEnrollment enrollment = CourseEnrollment.EnrollByAdmin(
                person.Id,
                course.Id,
                901,
                1,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                CourseRefundPolicyDefaults.BeforeStartPercentage,
                CourseRefundPolicyDefaults.AfterStartPercentage).Value;
            dbContext.Add(enrollment);
            await dbContext.SaveChangesAsync();

            Bill bill = Bill.IssueForCourseEnrollment(
                enrollment.Id,
                "INSTALLMENT-BILL-801",
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                dueDate,
                outstandingAmount).Value;
            dbContext.Add(bill);
            await dbContext.SaveChangesAsync();
        }

        RecordingEmailNotificationQueue queue = new();
        FakeCoursePaymentPlanGateway paymentPlans = new(planTypeCode);
        ServiceCollection services = new();
        services.AddScoped(_ => new MoeDbContext(options, contributors));
        services.AddSingleton<IEmailDeliverySwitch>(new FixedEmailDeliverySwitch(mailEnabled));
        services.AddSingleton<IEmailBrandingProvider>(new FixedEmailBrandingProvider());
        services.AddSingleton<IEmailNotificationQueue>(queue);
        services.AddSingleton<IEmailNotificationScheduler>(sp => new RecordingEmailNotificationScheduler(
            queue,
            sp.GetRequiredService<IEmailDeliverySwitch>()));
        services.AddSingleton<ICoursePaymentPlanGateway>(paymentPlans);
        ServiceProvider provider = services.BuildServiceProvider();
        MissedInstallmentPaymentEmailWorker worker = new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(TodayUtc),
            NullLogger<MissedInstallmentPaymentEmailWorker>.Instance);

        return new TestContext(worker, queue, paymentPlans, provider);
    }

    private sealed record TestContext(
        MissedInstallmentPaymentEmailWorker Worker,
        RecordingEmailNotificationQueue Queue,
        FakeCoursePaymentPlanGateway PaymentPlans,
        ServiceProvider Provider) : IDisposable
    {
        public void Dispose() => Provider.Dispose();
    }

    private sealed class FakeCoursePaymentPlanGateway(string planTypeCode) : ICoursePaymentPlanGateway
    {
        public int Calls { get; private set; }

        public Task<CourseBillingPlan?> FindPlanAsync(
            long coursePaymentPlanId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<CourseBillingPlan?>(new CourseBillingPlan(
                coursePaymentPlanId,
                1,
                planTypeCode,
                planTypeCode == "INSTALLMENT" ? 2 : 1,
                1,
                true));
        }
    }

    private sealed class FixedEmailDeliverySwitch(bool isEnabled) : IEmailDeliverySwitch
    {
        public bool IsEnabled { get; } = isEnabled;
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class PersonOnlyModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            var builder = modelBuilder.Entity<Person>();
            builder.ToTable("Person", "person");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("PersonId");
            builder.Property(x => x.ExternalPersonReference).HasColumnName("MockPassPersonId");
            builder.Property(x => x.OfficialFullName).HasColumnName("FullName");
            builder.Ignore(x => x.RowVersion);
            builder.Ignore(x => x.DomainEvents);
        }
    }
}
