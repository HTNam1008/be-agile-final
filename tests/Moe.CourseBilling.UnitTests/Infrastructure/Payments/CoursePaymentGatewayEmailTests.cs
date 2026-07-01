using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
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
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailQueue);

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-03");
        job.PersonId.Should().Be(1);
        job.Subject.Should().Be("You're Enrolled in Design Thinking 101");
        job.PlainTextBody.Should().Contain("Hello Course Student");
        job.PlainTextBody.Should().Contain("your enrolment in Design Thinking 101 is confirmed");
        job.PlainTextBody.Should().Contain("Course Start Date: 12 Aug 2026");
        job.HtmlBody.Should().Contain("View Course");
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_Should_Not_Fail_When_Email_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailQueue);

        Func<Task> act = () => gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailQueue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_Should_Not_Send_Noti03_For_Admin_Add()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.AdminAdd);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailQueue);

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendInstallmentEnrollmentConfirmationAsync_Should_Send_Noti03_Without_Payment_Received_Text()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (CourseEnrollment enrollment, _) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin,
            billCount: 2);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailQueue);

        await gateway.SendInstallmentEnrollmentConfirmationAsync(enrollment.Id, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Subject;
        job.NotificationType.Should().Be("NOTI-03");
        job.Subject.Should().Be("You're Enrolled in Design Thinking 101");
        job.PlainTextBody.Should().Contain("your enrolment in Design Thinking 101 is confirmed");
        job.PlainTextBody.Should().Contain("installment bills will be available in the payment dashboard");
        job.PlainTextBody.Should().NotContain("payment has been received");
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_For_Installment_Bill_Should_Not_Send_Noti03()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (CourseEnrollment enrollment, Bill firstBill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin,
            billCount: 2);
        CoursePaymentGateway gateway = CreateGateway(dbContext, mailQueue);

        await gateway.ApplySuccessfulPaymentAsync(
            firstBill.Id,
            firstBill.OutstandingAmount,
            paidInFull: false,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.Active);
        mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplySuccessfulPaymentAsync_WhenMailDeliveryDisabled_Should_Not_Enqueue_Email()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (CourseEnrollment enrollment, Bill bill) = await SeedEnrollmentAsync(
            dbContext,
            CourseEnrollmentSourceCodes.SelfJoin);
        CoursePaymentGateway gateway = CreateGateway(
            dbContext,
            mailQueue,
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        await gateway.ApplySuccessfulPaymentAsync(
            bill.Id,
            120m,
            paidInFull: true,
            Now,
            CancellationToken.None);

        enrollment.EnrollmentStatusCode.Should().Be(CourseEnrollmentStatusCodes.PaidInFull);
        mailQueue.Jobs.Should().BeEmpty();
    }

    private static async Task<(CourseEnrollment Enrollment, Bill Bill)> SeedEnrollmentAsync(
        MoeDbContext dbContext,
        string sourceCode,
        int billCount = 1)
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

        return (enrollment, bills[0]);
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
        IEmailNotificationQueue mailQueue,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            new TestDoubles.RecordingEmailNotificationScheduler(mailQueue, mailSwitch),
            new TestDoubles.FixedEmailBrandingProvider());

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            new CourseBillingModelConfiguration().Configure(modelBuilder);
        }
    }

}
