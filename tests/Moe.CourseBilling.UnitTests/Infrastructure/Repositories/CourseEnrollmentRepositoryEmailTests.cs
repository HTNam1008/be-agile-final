using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.People;
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
        FakeEmailDeliveryGateway mailGateway = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            person.Id,
            course.Id,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailGateway);

        await repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailGateway.Messages.Should().ContainSingle();
        EmailDeliveryMessage message = mailGateway.Messages.Single();
        message.ToEmail.Should().Be("student.real@example.com");
        message.Subject.Should().Be("You've Been Enrolled in Admin Course 101");
        message.PlainTextBody.Should().Contain("Hello Admin Added Student");
        message.PlainTextBody.Should().Contain("you have been enrolled in Admin Course 101 by your school administrator");
        message.PlainTextBody.Should().Contain("Fee Payable: To be confirmed after payment plan selection");
        message.PlainTextBody.Should().Contain("Go to Payment Dashboard");
        message.HtmlBody.Should().Contain("#DC343B");
    }

    [Fact]
    public async Task AddEnrollmentAsync_Should_Not_Fail_When_Email_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new()
        {
            ResultToReturn = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.EnrollByAdminPendingPlanSelection(
            person.Id,
            course.Id,
            adminLoginAccountId: 9001,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailGateway);

        Func<Task> act = () => repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        await act.Should().NotThrowAsync();
        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailGateway.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task AddEnrollmentAsync_Should_Not_Fail_When_Recipient_Is_Missing()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
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
            mailGateway,
            new TestDoubles.FixedEmailRecipientResolver(null));

        Func<Task> act = () => repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        await act.Should().NotThrowAsync();
        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AddEnrollmentAsync_Should_Not_Send_Noti04_For_Self_Join()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        (Person person, Course course) = await SeedPersonAndCourseAsync(dbContext);
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            person.Id,
            course.Id,
            coursePaymentPlanId: 10,
            loginAccountId: 1003,
            Now,
            100m,
            50m).Value;
        CourseEnrollmentRepository repository = CreateRepository(dbContext, mailGateway);

        await repository.AddEnrollmentAsync(enrollment, CancellationToken.None);

        dbContext.Set<CourseEnrollment>().Should().ContainSingle();
        mailGateway.Messages.Should().BeEmpty();
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
        FakeEmailDeliveryGateway mailGateway,
        IEmailRecipientResolver? recipientResolver = null)
        => new(
            dbContext,
            recipientResolver ?? new TestDoubles.FixedEmailRecipientResolver(),
            mailGateway,
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
}
