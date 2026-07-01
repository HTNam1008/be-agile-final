using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Payments;

public sealed class CoursePaymentGatewayEmailTests
{
    private static readonly DateTime Now = new(2026, 6, 29, 4, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly StartDate = new(2026, 8, 12);

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_Should_Send_SelfJoin_Enrollment_Success_Email()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailGateway);

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailGateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = mailGateway.Messages.Single();
        message.ToEmail.Should().Be("student.real@example.com");
        message.Subject.Should().Be("You're Enrolled in Design Thinking 101");
        message.PlainTextBody.Should().Contain("Hello Course Student");
        message.PlainTextBody.Should().Contain("your enrolment in Design Thinking 101 is confirmed");
        message.PlainTextBody.Should().Contain("Course Start Date: 12 Aug 2026");
        message.HtmlBody.Should().Contain("View Course");
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_Should_Not_Fail_When_Email_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new()
        {
            ResultToReturn = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailGateway);

        Func<Task> act = () => gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailGateway.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_Should_Not_Send_Noti03_For_Admin_Add()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.AdminAdd);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailGateway);

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_WhenMailDeliveryDisabled_Should_Not_Call_RecipientResolver_Or_Gateway()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(
            dbContext,
            mailGateway,
            new ThrowingEmailRecipientResolver(),
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailGateway.Messages.Should().BeEmpty();
    }

    private static async Task<(CourseEnrollment Enrollment, Bill Bill)> SeedEnrollmentAsync(
        MoeDbContext dbContext,
        string sourceCode)
    {
        Person person = new(
            1,
            "EXT-COURSE-1",
            "Course Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null);
        Course course = new(
            organizationId: 2,
            courseCode: "DT101",
            courseName: "Design Thinking 101",
            description: "Course enrollment email test",
            startDate: StartDate,
            endDate: StartDate.AddMonths(3),
            enrollmentOpenAtUtc: Now.AddDays(-5),
            enrollmentCloseAtUtc: Now.AddDays(5),
            actorLoginAccountId: 99,
            utcNow: Now);

        dbContext.Set<Person>().Add(person);
        dbContext.Set<Course>().Add(course);
        await dbContext.SaveChangesAsync();

        CourseEnrollment enrollment = sourceCode == CourseEnrollmentSourceCodes.SelfJoin
            ? CourseEnrollment.JoinSelf(person.Id, course.Id, 10, 1003, Now, 100m, 50m).Value
            : CourseEnrollment.EnrollByAdmin(person.Id, course.Id, 10, 9001, Now, 100m, 50m).Value;

        dbContext.Set<CourseEnrollment>().Add(enrollment);
        await dbContext.SaveChangesAsync();

        Bill bill = Bill.IssueForCourseEnrollment(
            enrollment.Id,
            $"BILL-{Guid.NewGuid():N}"[..30],
            Now,
            DateOnly.FromDateTime(Now),
            120m).Value;
        dbContext.Set<Bill>().Add(bill);
        await dbContext.SaveChangesAsync();

        return (enrollment, bill);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private static CoursePaymentGateway CreateGateway(
        MoeDbContext dbContext,
        FakeEmailDeliveryGateway mailGateway,
        IEmailRecipientResolver? recipientResolver = null,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            recipientResolver ?? new TestDoubles.FixedEmailRecipientResolver(),
            mailGateway,
            new FakeStudentNotificationRecipientResolver(),
            new FakeNotificationWriter(),
            mailSwitch ?? new TestDoubles.FixedEmailDeliverySwitch(),
            NullLogger<CoursePaymentGateway>.Instance);

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            new CourseBillingModelConfiguration().Configure(modelBuilder);
        }
    }

    private sealed class FakeEmailDeliveryGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Result ResultToReturn { get; init; } = Result.Success();

        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(ResultToReturn);
        }
    }

    private sealed class FakeStudentNotificationRecipientResolver : IStudentNotificationRecipientResolver
    {
        public Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(1001);
    }

    private sealed class FakeNotificationWriter : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Result<long>.Success(1));
    }

    private sealed class ThrowingEmailRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(
            long personId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");

        public EmailRecipient? ResolveProvided(string? providedEmail)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");
    }
}
