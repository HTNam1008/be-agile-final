using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.Infrastructure;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.Payments;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.MailDelivery;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.Notifications.IGateway.Notifications;
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
        RecordingScheduler mail = new();
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(db)
            .AddSingleton<IEmailDeliverySwitch>(new EnabledMailSwitch())
            .AddSingleton<ICoursePaymentPlanGateway>(new InstallmentPlanGateway())
            .AddSingleton<IEmailNotificationScheduler>(mail)
            .AddSingleton<IEmailBrandingProvider>(new FixedBrandingProvider())
            .BuildServiceProvider();
        MissedInstallmentPaymentEmailWorker worker = new(
            services.GetRequiredService<IServiceScopeFactory>(),
            new TestClock(SgtEarlyMorning),
            NullLogger<MissedInstallmentPaymentEmailWorker>.Instance,
            Options.Create(new CourseBillingWorkerOptions()));
        MethodInfo send = typeof(MissedInstallmentPaymentEmailWorker).GetMethod(
            "SendDueNotificationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)send.Invoke(worker, [CancellationToken.None])!;

        mail.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task SelfJoinCourseHandler_passes_singapore_business_day_to_fas_subsidy_gateway()
    {
        RecordingFasGateway fas = new();
        FakeEnrollmentRepository enrollments = new(CreatePublishedCourse());
        SelfJoinCourseHandler handler = new(
            enrollments,
            new FixedPaymentPlanGateway(),
            new NoopCoursePaymentGateway(),
            fas,
            new FakeCurrentUser(),
            new FakeStudentAccess(),
            new FakeStudentDirectory(),
            new FakeStudentNotificationRecipientResolver(),
            new FakeNotificationWriter(),
            new TestClock(SgtEarlyMorning));

        await handler.Handle(new SelfJoinCourseCommand(100, 200, [77]), CancellationToken.None);

        fas.ObservedEnrolledDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"coursebilling-sgt-red-{Guid.NewGuid():N}")
            .Options;
        return new MoeDbContext(options, [
            new CourseBillingModelConfiguration(),
            new IdentityPlatformModelConfiguration(),
            new MailDeliveryModelConfiguration()
        ]);
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

    private sealed class FakeStudentDirectory : IStudentDirectory
    {
        public Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken) =>
            Task.FromResult<StudentSummary?>(new(personId, "Student One", new DateOnly(2008, 1, 1), true, "School"));

        public Task<IReadOnlyCollection<long>> FindActivePersonIdsByOrganizationAsync(long organizationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<long>>([]);

        public Task<IReadOnlyList<AdminStudentSearchSummary>> ListByOrganizationAsync(AdminStudentSearchCriteria criteria, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminStudentSearchSummary>>([]);

        public Task<long> CountByOrganizationAsync(AdminStudentSearchCriteria criteria, CancellationToken cancellationToken) =>
            Task.FromResult(0L);
    }

    private sealed class FakeStudentNotificationRecipientResolver : IStudentNotificationRecipientResolver
    {
        public Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(personId);
    }

    private sealed class FakeNotificationWriter : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<long>.Success(1));
    }

    private sealed class EnabledMailSwitch : IEmailDeliverySwitch
    {
        public bool IsEnabled => true;
    }

    private sealed class FixedBrandingProvider : IEmailBrandingProvider
    {
        public string AppName => "Ministry of Education - Singapore";
        public string PaymentDashboardUrl => "https://portal.example.test/portal/payments";
        public string FasPortalUrl => "http://localhost:5173/portal/fas";
        public string AccountPortalUrl => "http://localhost:5173/portal/account";
        public string CourseDetailUrl(long courseId) => $"http://localhost:5173/portal/courses/{courseId}";
    }

    private sealed class FixedRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(long personId, CancellationToken cancellationToken) => Task.FromResult<EmailRecipient?>(new("student@example.com", EmailRecipientSourceCodes.Contact));
    }

    private sealed class NoopCoursePaymentGateway : ICoursePaymentGateway
    {
        public Task<PayableCourseBill?> FindPayableBillAsync(long billId, long personId, CancellationToken cancellationToken) => Task.FromResult<PayableCourseBill?>(null);
        public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken) => Task.FromResult<long?>(10);
        public Task ApplySuccessfulPaymentAsync(long billId, decimal amount, bool paidInFull, DateTime paidAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendInstallmentEnrollmentConfirmationAsync(long courseEnrollmentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ApplyPaymentFailureAsync(long billId, string failureReason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ApplyFullRefundForBillsAsync(IReadOnlyCollection<long> billIds, DateTime refundedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PayableStatement?> FindPayableStatementAsync(long statementId, long personId, CancellationToken cancellationToken) => Task.FromResult<PayableStatement?>(null);
        public Task ApplyStatementPaymentAsync(long statementId, IReadOnlyCollection<BillPaymentAllocation> allocations, DateTime paidAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<PaymentCheckoutLineItem>> BuildPaymentCheckoutLineItemsAsync(IReadOnlyCollection<long> billIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<PaymentCheckoutLineItem>>([]);
        public Task<Result> DeferStatementAsync(long statementId, long personId, IReadOnlyCollection<long> billIds, long actorLoginAccountId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
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

    private sealed class RecordingScheduler : IEmailNotificationScheduler
    {
        public bool IsEnabled => true;

        public List<EmailNotificationJob> Jobs { get; } = [];

        public Task<bool> EnqueueForPersonAsync(
            string notificationType,
            long personId,
            string subject,
            string plainTextBody,
            string? htmlBody,
            string? entityType,
            string? entityId,
            CancellationToken cancellationToken)
        {
            Jobs.Add(EmailNotificationJob.ForPerson(
                notificationType,
                personId,
                subject,
                plainTextBody,
                htmlBody,
                entityType,
                entityId));
            return Task.FromResult(true);
        }
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
