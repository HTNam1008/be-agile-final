using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Application.Enrollments.SelfJoinCourse;
using Moe.Modules.CourseBilling.Contracts.Enrollments;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Fas;
using Moe.Modules.CourseBilling.IGateway.Payments;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Domain;
using Moe.SharedKernel.Results;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Application.Enrollments.SelfJoinCourse;

public sealed class SelfJoinCourseHandlerEmailTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 4, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenSelfJoinInstallmentSucceeds_SendsEnrollmentConfirmation()
    {
        Course course = new(
            organizationId: 20,
            courseCode: "DT101",
            courseName: "Design Thinking 101",
            description: null,
            startDate: new DateOnly(2026, 8, 12),
            endDate: new DateOnly(2026, 11, 12),
            enrollmentOpenAtUtc: Now.AddDays(-1),
            enrollmentCloseAtUtc: Now.AddDays(1),
            actorLoginAccountId: 9001,
            utcNow: Now);
        course.Publish(9001, Now);
        SetId(course, 100);

        RecordingCoursePaymentGateway coursePayments = new();
        EnrollmentRepositoryDouble enrollments = new(course);
        SelfJoinCourseHandler handler = new(
            enrollments,
            new PaymentPlanGatewayDouble(new CourseBillingPlan(
                CoursePaymentPlanId: 300,
                CourseId: course.Id,
                PlanTypeCode: "INSTALLMENT",
                InstallmentCount: 3,
                IntervalMonths: 1,
                IsActive: true)),
            coursePayments,
            new FasGatewayDouble(),
            new CurrentUserDouble(),
            new StudentAccessDouble(),
            new StudentDirectoryDouble(),
            new StudentNotificationRecipientResolverDouble(),
            new NotificationWriterDouble(),
            new FixedClock(Now),
            NullLogger<SelfJoinCourseHandler>.Instance);

        Result<CourseEnrollmentResponse> result = await handler.Handle(
            new SelfJoinCourseCommand(course.Id, 300),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        coursePayments.InstallmentEnrollmentIds.Should().ContainSingle();
        enrollments.LastFirstDueDate.Should().Be(new DateOnly(2026, 8, 8));
    }

    [Fact]
    public async Task Handle_WhenNotificationWriterThrows_StillCompletesEnrollment()
    {
        Course course = new(
            organizationId: 20,
            courseCode: "DT101",
            courseName: "Design Thinking 101",
            description: null,
            startDate: new DateOnly(2026, 8, 12),
            endDate: new DateOnly(2026, 11, 12),
            enrollmentOpenAtUtc: Now.AddDays(-1),
            enrollmentCloseAtUtc: Now.AddDays(1),
            actorLoginAccountId: 9001,
            utcNow: Now);
        course.Publish(9001, Now);
        SetId(course, 100);

        RecordingCoursePaymentGateway coursePayments = new();
        EnrollmentRepositoryDouble enrollments = new(course);
        SelfJoinCourseHandler handler = new(
            enrollments,
            new PaymentPlanGatewayDouble(new CourseBillingPlan(
                CoursePaymentPlanId: 300,
                CourseId: course.Id,
                PlanTypeCode: "INSTALLMENT",
                InstallmentCount: 3,
                IntervalMonths: 1,
                IsActive: true)),
            coursePayments,
            new FasGatewayDouble(),
            new CurrentUserDouble(),
            new StudentAccessDouble(),
            new StudentDirectoryDouble(),
            new StudentNotificationRecipientResolverDouble(),
            new NotificationWriterDouble(throwOnCreate: true),
            new FixedClock(Now),
            NullLogger<SelfJoinCourseHandler>.Instance);

        Result<CourseEnrollmentResponse> result = await handler.Handle(
            new SelfJoinCourseCommand(course.Id, 300),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        coursePayments.InstallmentEnrollmentIds.Should().ContainSingle();
        enrollments.LastFirstDueDate.Should().Be(new DateOnly(2026, 8, 8));
    }

    private static void SetId<T>(Entity<T> entity, T id) where T : notnull
        => typeof(Entity<T>)
            .GetProperty(nameof(Entity<T>.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(entity, id);

    private sealed class EnrollmentRepositoryDouble(Course course) : ICourseEnrollmentRepository
    {
        public DateOnly? LastFirstDueDate { get; private set; }

        public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(course.OrganizationId);

        public Task<Course?> FindCourseAsync(long courseId, CancellationToken cancellationToken)
            => Task.FromResult<Course?>(course);

        public Task<bool> PersonExistsAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<long?> FindActiveStudentPersonIdAsync(
            string studentNumber,
            long organizationId,
            DateOnly onDate,
            CancellationToken cancellationToken)
            => Task.FromResult<long?>(2001);

        public Task<bool> PersonHasActiveSchoolEnrollmentAsync(
            long personId,
            long organizationId,
            DateOnly onDate,
            CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<bool> ExistsAsync(long personId, long courseId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<IReadOnlyCollection<CourseFeeBillingLine>> ListActiveCourseFeesAsync(
            long courseId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<CourseFeeBillingLine>>(
            [
                new CourseFeeBillingLine(
                    CourseFeeId: 1,
                    FeeComponentId: 10,
                    FeeComponentName: "Course Fee",
                    CalculationTypeCode: "FIXED",
                    IsTaxComponent: false,
                    FeeValue: 300m)
            ]);

        public Task AddEnrollmentAsync(CourseEnrollment enrollment, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<CourseEnrollmentBillingResult> AddEnrollmentAndIssueBillsAsync(
            CourseEnrollment enrollment,
            string billNumberPrefix,
            DateTime issuedAtUtc,
            DateOnly firstDueDate,
            int installmentCount,
            int intervalMonths,
            IReadOnlyCollection<CourseFeeBillingLine> feeLines,
            IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
            CancellationToken cancellationToken)
        {
            LastFirstDueDate = firstDueDate;
            SetId(enrollment, 500);
            Bill bill = Bill.IssueForCourseEnrollment(
                enrollment.Id,
                $"{billNumberPrefix}-001",
                issuedAtUtc,
                firstDueDate,
                100m,
                sequenceNumber: 1).Value;
            SetId(bill, 700);
            return Task.FromResult(new CourseEnrollmentBillingResult(
                enrollment,
                [new GeneratedBillResult(bill, BillLineCount: 1)]));
        }

        public Task<CourseEnrollment?> FindEnrollmentAsync(
            long enrollmentId,
            long personId,
            CancellationToken cancellationToken)
            => Task.FromResult<CourseEnrollment?>(null);

        public Task<CourseEnrollmentBillingResult?> ChangePaymentPlanAndReissueBillsAsync(
            CourseEnrollment enrollment,
            long coursePaymentPlanId,
            bool installment,
            string billNumberPrefix,
            DateTime issuedAtUtc,
            DateOnly firstDueDate,
            int installmentCount,
            int intervalMonths,
            IReadOnlyCollection<CourseFeeBillingLine> feeLines,
            IReadOnlyCollection<CourseFasSubsidy> fasSubsidies,
            CancellationToken cancellationToken)
            => Task.FromResult<CourseEnrollmentBillingResult?>(null);

        public CourseEnrollmentBillingPreviewResult PreviewPaymentPlanBills(
            CourseBillingPlan plan,
            bool installment,
            DateOnly firstDueDate,
            IReadOnlyCollection<CourseFeeBillingLine> feeLines,
            IReadOnlyCollection<CourseFasSubsidy> fasSubsidies)
            => throw new NotSupportedException();
    }

    private sealed class PaymentPlanGatewayDouble(CourseBillingPlan plan) : ICoursePaymentPlanGateway
    {
        public Task<CourseBillingPlan?> FindPlanAsync(long coursePaymentPlanId, CancellationToken cancellationToken)
            => Task.FromResult<CourseBillingPlan?>(plan);
    }

    private sealed class RecordingCoursePaymentGateway : ICoursePaymentGateway
    {
        public List<long> InstallmentEnrollmentIds { get; } = [];

        public Task<PayableCourseBill?> FindPayableBillAsync(long billId, long personId, CancellationToken cancellationToken)
            => Task.FromResult<PayableCourseBill?>(null);

        public Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(null);

        public Task<IReadOnlyCollection<BillSchoolOrganization>> FindBillOrganizationIdsAsync(
            IReadOnlyCollection<long> billIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<BillSchoolOrganization>>([]);

        public Task ApplySuccessfulPaymentAsync(
            long billId,
            decimal amount,
            bool paidInFull,
            DateTime paidAtUtc,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendInstallmentEnrollmentConfirmationAsync(
            long courseEnrollmentId,
            CancellationToken cancellationToken)
        {
            InstallmentEnrollmentIds.Add(courseEnrollmentId);
            return Task.CompletedTask;
        }

        public Task ApplyPaymentFailureAsync(long billId, string failureReason, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ApplyFullRefundForBillsAsync(
            IReadOnlyCollection<long> billIds,
            DateTime refundedAtUtc,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<PayableStatement?> FindPayableStatementAsync(
            long statementId,
            long personId,
            CancellationToken cancellationToken)
            => Task.FromResult<PayableStatement?>(null);

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
            => Task.FromResult(Result.Success());
    }

    private sealed class FasGatewayDouble : IFasCourseSubsidyGateway
    {
        public Task<IReadOnlyCollection<CourseFasSubsidy>> ListEligibleSubsidiesAsync(
            long personId,
            long courseId,
            DateOnly enrolledDate,
            IReadOnlyCollection<long>? fasApplicationSchemeIds,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<CourseFasSubsidy>>([]);

        public Task RecordPendingRedemptionsAsync(
            long personId,
            long courseId,
            long courseEnrollmentId,
            long billId,
            decimal totalSubsidyAmount,
            IReadOnlyCollection<CourseFasSubsidy> subsidies,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RedeemPendingRedemptionsForBillsAsync(
            IReadOnlyCollection<long> billIds,
            DateTime redeemedAtUtc,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task CancelPendingRedemptionsForEnrollmentAsync(
            long courseEnrollmentId,
            DateTime cancelledAtUtc,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class CurrentUserDouble : ICurrentUser
    {
        public long? UserAccountId => 1003;
        public long? PersonId => 2001;
        public long? OrganizationUnitId => 20;
        public IReadOnlyCollection<long> OrganizationUnitIds => [20];
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "Student";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => false;
    }

    private sealed class StudentAccessDouble : IStudentAccessControl
    {
        public long? PersonId => 2001;
        public bool IsStudent => true;
        public bool CanAccessOwnPerson(long personId) => personId == PersonId;
        public Task<bool> CanUseSchoolServiceAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class StudentDirectoryDouble : IStudentDirectory
    {
        public Task<StudentSummary?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<StudentSummary?>(new(personId, "Course Student", new DateOnly(2008, 2, 1), true, "School"));

        public Task<IReadOnlyCollection<long>> FindActivePersonIdsByOrganizationAsync(long organizationId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<long>>([2001L]);

        public Task<IReadOnlyList<AdminStudentSearchSummary>> ListByOrganizationAsync(AdminStudentSearchCriteria criteria, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AdminStudentSearchSummary>>([]);

        public Task<long> CountByOrganizationAsync(AdminStudentSearchCriteria criteria, CancellationToken cancellationToken)
            => Task.FromResult(0L);
    }

    private sealed class StudentNotificationRecipientResolverDouble : IStudentNotificationRecipientResolver
    {
        public Task<long?> FindUserAccountIdByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<long?>(1003);
    }

    private sealed class NotificationWriterDouble(bool throwOnCreate = false) : INotificationWriter
    {
        public Task<Result<long>> CreateAsync(NotificationCreateRequest request, CancellationToken cancellationToken = default)
        {
            if (throwOnCreate)
            {
                throw new InvalidOperationException("Notification infrastructure unavailable.");
            }

            return Task.FromResult(Result<long>.Success(1));
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(utcNow, TimeSpan.Zero);

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
