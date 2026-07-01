using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests;

public sealed class SingaporeBusinessDayRedTests
{
    private static readonly DateTimeOffset SgtEarlyMorning =
        new(2026, 6, 30, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task MissedInstallmentPaymentEmailWorker_uses_singapore_business_day_for_missed_due_date()
    {
        await using MoeDbContext db = CreateDbContext();
        Course course = CreatePublishedCourse();
        db.Add(course);
        db.Add(new Person(1, "P1", "Student One", new DateOnly(2008, 1, 1), "SG", "CITIZEN"));
        await db.SaveChangesAsync();
        CourseEnrollment enrollment = CourseEnrollment.JoinSelf(
            1,
            course.Id,
            200,
            99,
            SgtEarlyMorning.UtcDateTime,
            CourseRefundPolicyDefaults.BeforeStartPercentage,
            CourseRefundPolicyDefaults.AfterStartPercentage).Value;
        db.Add(enrollment);
        await db.SaveChangesAsync();
        db.Add(Bill.IssueForCourseEnrollment(
            enrollment.Id,
            "BILL-001",
            SgtEarlyMorning.UtcDateTime,
            new DateOnly(2026, 6, 30),
            100m).Value);
        await db.SaveChangesAsync();
        RecordingEmailGateway mail = new();
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailDeliverySwitch>(new EnabledMailSwitch())
            .AddSingleton<ICoursePaymentPlanGateway>(new InstallmentPlanGateway())
            .AddSingleton<IEmailRecipientResolver>(new FixedRecipientResolver())
            .AddSingleton<IEmailDeliveryGateway>(mail)
            .BuildServiceProvider();
        MissedInstallmentPaymentEmailWorker worker = new(
            services.GetRequiredService<IServiceScopeFactory>(),
            new TestClock(SgtEarlyMorning),
            NullLogger<MissedInstallmentPaymentEmailWorker>.Instance);
        MethodInfo send = typeof(MissedInstallmentPaymentEmailWorker).GetMethod(
            "SendDueNotificationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)send.Invoke(worker, [CancellationToken.None])!;

        mail.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task SelfJoinCourseHandler_passes_singapore_business_day_to_fas_subsidy_gateway()
    {
        RecordingFasGateway fas = new();
        FakeEnrollmentRepository enrollments = new(CreatePublishedCourse());
        SelfJoinCourseHandler handler = new(
            enrollments,
            new FixedPaymentPlanGateway(),
            fas,
            new FakeCurrentUser(),
            new FakeStudentAccess(),
            new TestClock(SgtEarlyMorning));

        await handler.Handle(new SelfJoinCourseCommand(100, 200, [77]), CancellationToken.None);

        fas.ObservedEnrolledDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"coursebilling-sgt-red-{Guid.NewGuid():N}")
            .Options;
        return new MoeDbContext(options, [new CourseBillingModelConfiguration(), new IdentityPlatformModelConfiguration()]);
    }

    private static Course CreatePublishedCourse()
    {
        Course course = new(
            10,
            "COURSE-1",
            "Course One",
            null,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            99,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        course.Publish(99, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return course;
    }

    private sealed class FakeEnrollmentRepository(Course course) : ICourseEnrollmentRepository
    {
        public Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken) => Task.FromResult<Course?>(course);
        public Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyCollection<CourseFeeBillingLine>> ListActiveCourseFeesAsync(long courseId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<CourseFeeBillingLine>>([new(1, 1, "Tuition", "FIXED", false, 100m)]);
        public Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillsAsync(CourseEnrollment enrollment, string billNumberPrefix, DateTime issuedAtUtc, DateOnly firstDueDate, int installmentCount, int intervalMonths, IReadOnlyCollection<CourseFeeBillingLine> feeLines, IReadOnlyCollection<CourseFasSubsidy> fasSubsidies, CancellationToken cancellationToken)
        {
            Bill bill = Bill.IssueForCourseEnrollment(1, "BILL-SELF", issuedAtUtc, firstDueDate, 100m).Value;
            return Task.FromResult(new CourseEnrollmentBillingResult(enrollment, [new GeneratedBillResult(bill, 1)]));
        }

        public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken) => Task.FromResult<long?>(10);
        public Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<long?> FindActiveStudentPersonIdAsync(string studentNumber, long organizationId, DateOnly onDate, CancellationToken cancellationToken) => Task.FromResult<long?>(1);
        public Task<bool> PersonHasActiveSchoolEnrollmentAsync(long personId, long organizationId, DateOnly onDate, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CourseEnrollment?> FindEnrollmentAsync(long enrollmentId, long personId, CancellationToken cancellationToken) => Task.FromResult<CourseEnrollment?>(null);
        public Task<CourseEnrollmentBillingResult?> ChangePaymentPlanAndReissueBillsAsync(CourseEnrollment enrollment, long coursePaymentPlanId, bool installment, string billNumberPrefix, DateTime issuedAtUtc, DateOnly firstDueDate, int installmentCount, int intervalMonths, IReadOnlyCollection<CourseFeeBillingLine> feeLines, IReadOnlyCollection<CourseFasSubsidy> fasSubsidies, CancellationToken cancellationToken) => Task.FromResult<CourseEnrollmentBillingResult?>(null);
        public CourseEnrollmentBillingPreviewResult PreviewPaymentPlanBills(CourseBillingPlan plan, bool installment, DateOnly firstDueDate, IReadOnlyCollection<CourseFeeBillingLine> feeLines, IReadOnlyCollection<CourseFasSubsidy> fasSubsidies) => throw new NotSupportedException();
    }

    private sealed class RecordingFasGateway : IFasCourseSubsidyGateway
    {
        public DateOnly? ObservedEnrolledDate { get; private set; }
        public Task<IReadOnlyCollection<CourseFasSubsidy>> ListEligibleSubsidiesAsync(long personId, long courseId, DateOnly enrolledDate, IReadOnlyCollection<long>? fasApplicationSchemeIds, CancellationToken cancellationToken)
        {
            ObservedEnrolledDate = enrolledDate;
            return Task.FromResult<IReadOnlyCollection<CourseFasSubsidy>>([new(77, "FIXED", 10m)]);
        }

        public Task RecordPendingRedemptionsAsync(long personId, long courseId, long courseEnrollmentId, long billId, decimal totalSubsidyAmount, IReadOnlyCollection<CourseFasSubsidy> subsidies, DateTime utcNow, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RedeemPendingRedemptionsForBillsAsync(IReadOnlyCollection<long> billIds, DateTime redeemedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CancelPendingRedemptionsForEnrollmentAsync(long courseEnrollmentId, DateTime cancelledAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedPaymentPlanGateway : ICoursePaymentPlanGateway
    {
        public Task<CourseBillingPlan?> FindPlanAsync(long coursePaymentPlanId, CancellationToken cancellationToken) =>
            Task.FromResult<CourseBillingPlan?>(new(coursePaymentPlanId, 100, "FULL", 1, 1, true));
    }

    private sealed class InstallmentPlanGateway : ICoursePaymentPlanGateway
    {
        public Task<CourseBillingPlan?> FindPlanAsync(long coursePaymentPlanId, CancellationToken cancellationToken) =>
            Task.FromResult<CourseBillingPlan?>(new(coursePaymentPlanId, 1, "INSTALLMENT", 3, 1, true));
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => 1;
        public long? OrganizationUnitId => 10;
        public IReadOnlyCollection<long> OrganizationUnitIds => [10];
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "EService";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => false;
    }

    private sealed class FakeStudentAccess : IStudentAccessControl
    {
        public bool IsStudent => true;
        public long? PersonId => 1;
        public bool CanAccessOwnPerson(long personId) => personId == PersonId;
        public Task<bool> CanUseSchoolServiceAsync(long organizationId, CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class EnabledMailSwitch : IEmailDeliverySwitch
    {
        public bool IsEnabled => true;
    }

    private sealed class FixedRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(long personId, CancellationToken cancellationToken) => Task.FromResult<EmailRecipient?>(new("student@example.com", EmailRecipientSourceCodes.Preferred));
        public EmailRecipient? ResolveProvided(string? emailAddress) => null;
    }

    private sealed class RecordingEmailGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];
        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
