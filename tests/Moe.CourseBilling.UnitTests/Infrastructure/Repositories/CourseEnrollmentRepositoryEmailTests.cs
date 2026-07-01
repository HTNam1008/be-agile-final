using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Repositories;

public sealed class CourseEnrollmentRepositoryEmailTests
{
    private static readonly DateTime Now = new(2026, 6, 29, 4, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly StartDate = new(2026, 8, 12);

    [Fact]
    public async Task AddEnrollmentAsync_Should_Send_Email_For_Admin_Add()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            person.Id,
            course.Id,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailQueue);

        await repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-04");
        job.PersonId.Should().Be(1);
        job.Subject.Should().Be("You've Been Enrolled in Admin Course 101");
        job.PlainTextBody.Should().Contain("Hello Admin Added Student");
        job.PlainTextBody.Should().Contain("you have been enrolled in Admin Course 101 by your school administrator");
        job.PlainTextBody.Should().Contain("Fee Payable: To be confirmed after payment plan selection");
        job.PlainTextBody.Should().Contain("Go to Payment Dashboard");
        job.HtmlBody.Should().Contain("#DC343B");
    }

    [Fact]
    public async Task AddEnrollmentAndIssueBillsAsync_Should_Send_Admin_Add_Email_With_Bill_Values()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdmin(
            person.Id,
            course.Id,
            coursePaymentPlanId: 10,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailQueue);

        await repository.AddEnrollmentAndIssueBillsAsync(
            enrollment,
            "BILL-ADMIN-ADD",
            Now,
            new DateOnly(2026, 7, 15),
            installmentCount: 2,
            intervalMonths: 1,
            [
                new CourseFeeBillingLine(
                    CourseFeeId: 1,
                    FeeComponentId: 10,
                    FeeComponentName: "Course Fee",
                    CalculationTypeCode: "FIXED",
                    IsTaxComponent: false,
                    FeeValue: 120m)
            ],
            [],
            CancellationToken.None);

        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.PlainTextBody.Should().Contain("Fee Payable: SGD 120.00");
        job.PlainTextBody.Should().Contain("Payment Due Date: 15 Jul 2026");
        job.HtmlBody.Should().Contain("SGD 120.00");
        job.HtmlBody.Should().Contain("15 Jul 2026");
    }

    [Fact]
    public async Task AddEnrollmentAsync_Should_Not_Fail_When_Email_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            person.Id,
            course.Id,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailQueue);

        Func<Task> act = () => repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        await act.Should().NotThrowAsync();
        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailQueue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task AddEnrollmentAsync_WhenMailDeliveryDisabled_Should_Not_Enqueue_Email()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            person.Id,
            course.Id,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(
            dbContext,
            mailQueue,
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        await repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task AddEnrollmentAsync_Should_Not_Send_Noti04_For_Self_Join()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            person.Id,
            course.Id,
            coursePaymentPlanId: 10,
            loginAccountId: 1003,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailQueue);

        await repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailQueue.Jobs.Should().BeEmpty();
    }

    private static async Task<(Person Person, Course Course)> SeedPersonAndCourseAsync(MoeDbContext dbContext)
    {
        Person person = new(
            1,
            "EXT-ADMIN-ADD",
            "Admin Added Student",
            new DateOnly(2008, 2, 1),
            "SG",
            null);
        Course course = new(
            organizationId: 2,
            courseCode: "ADM101",
            courseName: "Admin Course 101",
            description: "Admin add email test",
            startDate: StartDate,
            endDate: StartDate.AddMonths(3),
            enrollmentOpenAtUtc: Now.AddDays(-5),
            enrollmentCloseAtUtc: Now.AddDays(5),
            actorLoginAccountId: 99,
            utcNow: Now);

        dbContext.Set<Person>().Add(person);
        dbContext.Set<Course>().Add(course);
        await dbContext.SaveChangesAsync();

        return (person, course);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private static CourseEnrollmentRepository CreateRepository(
        MoeDbContext dbContext,
        IEmailNotificationQueue mailQueue,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            mailQueue,
            mailSwitch ?? new TestDoubles.FixedEmailDeliverySwitch(),
            new TestDoubles.FixedEmailBrandingProvider(),
            NullLogger<CourseEnrollmentRepository>.Instance);

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<SchoolEnrollment>().HasKey(x => x.Id);
            new CourseBillingModelConfiguration().Configure(modelBuilder);
        }
    }

}
