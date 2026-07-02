using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class BillingStatementRepositoryEmailTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly TestDoubles.RecordingEmailNotificationQueue _mailQueue = new();
    private readonly BillingStatementRepository _repository;

    public BillingStatementRepositoryEmailTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"billing-statement-email-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(
            options,
            [
                new PersonOnlyModelConfiguration(),
                new CourseBillingModelConfiguration(),
                new MailDeliveryModelConfiguration()
            ]);

        _repository = new BillingStatementRepository(
            _dbContext,
            new FixedCoursePaymentPlanGateway(),
            new TestDoubles.RecordingEmailNotificationScheduler(_mailQueue),
            new TestDoubles.FixedEmailBrandingProvider());
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetOrCreateAsync_WithOutstandingBill_SendsMonthlyBillEmail()
    {
        await SeedStudentWithBillAsync(
            personId: 5001,
            studentName: "Ada Student",
            billAmount: 123.45m,
            dueDate: new DateOnly(2026, 7, 1));

        var statement = await _repository.GetOrCreateAsync(
            5001,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(123.45m);
        _mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = _mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-01");
        job.PersonId.Should().Be(5001);
        job.Subject.Should().Be("Your July Bill Is Ready");
        job.PlainTextBody.Should().Contain("Hello Ada Student, your consolidated bill for July 2026 is now ready.");
        job.PlainTextBody.Should().Contain("Total Amount Due: SGD 123.45");
        job.PlainTextBody.Should().Contain("Due Date: 01 Jul 2026");
        job.PlainTextBody.Should().Contain("http://localhost:5173/portal/payments");
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenStatementAlreadyGenerated_DoesNotSendDuplicateMonthlyBillEmail()
    {
        await SeedStudentWithBillAsync(
            personId: 5010,
            studentName: "Generated Student",
            billAmount: 54.20m,
            dueDate: new DateOnly(2026, 7, 1));

        await _repository.GetOrCreateAsync(
            5010,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        _mailQueue.Jobs.Should().ContainSingle();
        _mailQueue.Jobs.Clear();
        await SeedMonthlyBillNotificationAsync(5010, new DateOnly(2026, 7, 1));

        await _repository.GetOrCreateAsync(
            5010,
            2026,
            7,
            new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        _mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenStatementAlreadyGeneratedWithoutNotification_SendsMonthlyBillEmail()
    {
        await SeedStudentWithBillAsync(
            personId: 5012,
            studentName: "Existing Statement Student",
            billAmount: 64.80m,
            dueDate: new DateOnly(2026, 8, 1));

        await _repository.GetOrCreateAsync(
            5012,
            2026,
            8,
            new DateTime(2026, 7, 31, 23, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.Suppress,
            CancellationToken.None);

        _mailQueue.Jobs.Should().BeEmpty();

        await _repository.GetOrCreateAsync(
            5012,
            2026,
            8,
            new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        EmailNotificationJob job = _mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-01");
        job.PersonId.Should().Be(5012);
        job.EntityType.Should().Be("BillingStatement");
        job.EntityId.Should().Be("2026-08");
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenNotificationSuppressed_CreatesStatementAndSkipsEmail()
    {
        await SeedStudentWithBillAsync(
            personId: 5011,
            studentName: "Payment Flow Student",
            billAmount: 75m,
            dueDate: new DateOnly(2026, 7, 10));

        var statement = await _repository.GetOrCreateAsync(
            5011,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.Suppress,
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(75m);
        _mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_WithNoOutstandingBill_DoesNotSendEmail()
    {
        await SeedStudentWithBillAsync(
            personId: 5002,
            studentName: "Paid Student",
            billAmount: 0m,
            dueDate: new DateOnly(2026, 7, 1));

        var statement = await _repository.GetOrCreateAsync(
            5002,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(0m);
        _mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenEmailQueueFails_StillReturnsStatement()
    {
        _mailQueue.Result = Result.Failure(MailDeliveryErrors.QueueFull);
        await SeedStudentWithBillAsync(
            personId: 5003,
            studentName: "Mail Failure Student",
            billAmount: 88m,
            dueDate: new DateOnly(2026, 7, 15));

        var statement = await _repository.GetOrCreateAsync(
            5003,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(88m);
        _mailQueue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenMailDeliveryDisabled_StillReturnsStatementAndSkipsEmail()
    {
        BillingStatementRepository repository = new(
            _dbContext,
            new FixedCoursePaymentPlanGateway(),
            new TestDoubles.RecordingEmailNotificationScheduler(
                _mailQueue,
                new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false)),
            new TestDoubles.FixedEmailBrandingProvider());
        await SeedStudentWithBillAsync(
            personId: 5004,
            studentName: "Disabled Mail Student",
            billAmount: 42m,
            dueDate: new DateOnly(2026, 7, 15));

        var statement = await repository.GetOrCreateAsync(
            5004,
            2026,
            7,
            new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            BillingStatementNotificationMode.SendMonthlyBill,
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(42m);
        _mailQueue.Jobs.Should().BeEmpty();
    }

    private async Task SeedStudentWithBillAsync(
        long personId,
        string studentName,
        decimal billAmount,
        DateOnly dueDate)
    {
        Person person = new(
            personId,
            $"TEST-PERSON-{personId}",
            studentName,
            new DateOnly(2010, 1, 1),
            "SG",
            "CITIZEN");
        person.UpdatePreferredContact("student.real@example.com", null, null, new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        Course course = new(
            organizationId: 10,
            courseCode: $"COURSE-{personId}",
            courseName: "Billing Course",
            description: null,
            startDate: new DateOnly(2026, 7, 1),
            endDate: new DateOnly(2026, 8, 1),
            enrollmentOpenAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAtUtc: new DateTime(2026, 6, 30, 16, 0, 0, DateTimeKind.Utc),
            actorLoginAccountId: 42,
            utcNow: new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc));

        _dbContext.AddRange(person, course);
        await _dbContext.SaveChangesAsync();

        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdmin(
            personId,
            course.Id,
            coursePaymentPlanId: 100,
            adminLoginAccountId: 42,
            enrolledAtUtc: new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            beforeStartRefundPercentage: CourseRefundPolicyDefaults.BeforeStartPercentage,
            afterStartRefundPercentage: CourseRefundPolicyDefaults.AfterStartPercentage).Value;

        _dbContext.Add(enrollment);
        await _dbContext.SaveChangesAsync();

        Bill bill = Bill.IssueForCourseEnrollment(
            enrollment.Id,
            $"BILL-{personId}",
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            dueDate,
            billAmount).Value;

        _dbContext.Add(bill);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedMonthlyBillNotificationAsync(long personId, DateOnly billingMonth)
    {
        _dbContext.Set<EmailNotification>().Add(EmailNotification.Create(
            "NOTI-01",
            personId,
            $"Your {billingMonth:MMMM} Bill Is Ready",
            "Already scheduled.",
            null,
            "BillingStatement",
            $"{billingMonth.Year:D4}-{billingMonth.Month:D2}",
            new DateTime(billingMonth.Year, billingMonth.Month, 1, 8, 0, 0, DateTimeKind.Utc),
            3));
        await _dbContext.SaveChangesAsync();
    }

    private sealed class FixedCoursePaymentPlanGateway : ICoursePaymentPlanGateway
    {
        public Task<CourseBillingPlan?> FindPlanAsync(
            long coursePaymentPlanId,
            CancellationToken cancellationToken)
            => Task.FromResult<CourseBillingPlan?>(new CourseBillingPlan(
                coursePaymentPlanId,
                CourseId: 1,
                PlanTypeCode: "INSTALLMENT",
                InstallmentCount: 3,
                IntervalMonths: 1,
                IsActive: true));
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
