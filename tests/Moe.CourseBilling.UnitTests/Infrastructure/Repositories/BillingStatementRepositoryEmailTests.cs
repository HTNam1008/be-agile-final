using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.Domain;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class BillingStatementRepositoryEmailTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly RecordingEmailDeliveryGateway _mailGateway = new();
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
                new CourseBillingModelConfiguration()
            ]);

        _repository = new BillingStatementRepository(
            _dbContext,
            new FixedCoursePaymentPlanGateway(),
            new TestDoubles.FixedEmailRecipientResolver(),
            _mailGateway,
            new TestDoubles.FixedEmailDeliverySwitch(),
            NullLogger<BillingStatementRepository>.Instance);
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
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(123.45m);
        _mailGateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = _mailGateway.Messages.Single();
        message.ToEmail.Should().Be("student.real@example.com");
        message.Subject.Should().Be("Your July Bill Is Ready");
        message.PlainTextBody.Should().Contain("Hello Ada Student, your consolidated bill for July 2026 is now ready.");
        message.PlainTextBody.Should().Contain("Total Amount Due: SGD 123.45");
        message.PlainTextBody.Should().Contain("Due Date: 01 Jul 2026");
        message.PlainTextBody.Should().Contain("http://localhost:5173/portal/payments");
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
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(0m);
        _mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenEmailFails_StillReturnsStatement()
    {
        _mailGateway.Result = Result.Failure(MailDeliveryErrors.MissingSmtpPassword);
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
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(88m);
        _mailGateway.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenMailDeliveryDisabled_StillReturnsStatementAndSkipsEmail()
    {
        BillingStatementRepository repository = new(
            _dbContext,
            new FixedCoursePaymentPlanGateway(),
            new TestDoubles.FixedEmailRecipientResolver(),
            _mailGateway,
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false),
            NullLogger<BillingStatementRepository>.Instance);
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
            CancellationToken.None);

        statement.OutstandingAmount.Should().Be(42m);
        _mailGateway.Messages.Should().BeEmpty();
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

    private sealed class RecordingEmailDeliveryGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];
        public Result Result { get; set; } = Result.Success();

        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result);
        }
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
