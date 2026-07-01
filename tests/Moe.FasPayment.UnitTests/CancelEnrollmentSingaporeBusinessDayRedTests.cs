using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Application.EnrollmentCancellations;
using Moe.Modules.FasPayment.Contracts.Payments;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.FasPayment.UnitTests;

public sealed class CancelEnrollmentSingaporeBusinessDayRedTests
{
    private static readonly DateTimeOffset SgtEarlyMorning =
        new(2026, 6, 30, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CancelEnrollment_uses_singapore_business_day_for_started_course_policy()
    {
        FakeCancellationRepository cancellations = new();
        CancelEnrollmentHandler handler = new(
            new FakeCurrentUser(),
            new FakePreviewRepository(CreateSnapshot()),
            new NoopRefundProcessor(),
            cancellations,
            new TestClock(SgtEarlyMorning),
            CreateEmailService());

        Result<EnrollmentCancellationResponse> result = await handler.Handle(
            new CancelEnrollmentCommand(500, new CancelEnrollmentRequest("sgt-red")),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        cancellations.Called.Should().BeFalse();
    }

    private static EnrollmentCancellationSnapshot CreateSnapshot()
    {
        object course = CreateCourse();
        object enrollment = CreateEnrollment();
        ConstructorInfo ctor = typeof(EnrollmentCancellationSnapshot).GetConstructors().Single();
        return (EnrollmentCancellationSnapshot)ctor.Invoke([
            enrollment,
            course,
            100m,
            0m,
            100m,
            25m,
            1,
            Array.Empty<EnrollmentPaymentRefundSource>()
        ]);
    }

    private static object CreateCourse()
    {
        Type courseType = Type.GetType("Moe.Modules.CourseBilling.Domain.Courses.Course, Moe.Modules.CourseBilling")!;
        return Activator.CreateInstance(
            courseType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                10L,
                "COURSE-SGT",
                "Course SGT",
                null,
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 7, 31),
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                99L,
                SgtEarlyMorning.UtcDateTime,
                100m,
                50m
            ],
            culture: null)!;
    }

    private static object CreateEnrollment()
    {
        Type enrollmentType = Type.GetType("Moe.Modules.CourseBilling.Domain.Courses.CourseEnrollment, Moe.Modules.CourseBilling")!;
        MethodInfo joinSelf = enrollmentType.GetMethod(
            "JoinSelf",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        object result = joinSelf.Invoke(null, [1L, 100L, 200L, 99L, SgtEarlyMorning.UtcDateTime, 100m, 50m])!;
        return result.GetType().GetProperty("Value")!.GetValue(result)!;
    }

    private static CourseWithdrawalEmailService CreateEmailService()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"cancel-sgt-red-{Guid.NewGuid():N}")
            .Options;
        return new CourseWithdrawalEmailService(
            new MoeDbContext(options, []),
            new NullRecipientResolver(),
            new NoopEmailGateway(),
            new DisabledMailSwitch(),
            NullLogger<CourseWithdrawalEmailService>.Instance);
    }

    private sealed class FakePreviewRepository(EnrollmentCancellationSnapshot snapshot) : IEnrollmentRefundPreviewRepository
    {
        public Task<EnrollmentCancellationSnapshot?> FindAsync(long enrollmentId, long personId, CancellationToken cancellationToken) =>
            Task.FromResult<EnrollmentCancellationSnapshot?>(snapshot);
    }

    private sealed class FakeCancellationRepository : IEnrollmentCancellationRepository
    {
        public bool Called { get; private set; }
        public Task<Result<string>> CancelEnrollmentAndOutstandingBillsAsync(long enrollmentId, long personId, bool refunded, DateTime utcNow, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(Result<string>.Success("CANCELLED"));
        }
    }

    private sealed class NoopRefundProcessor : IEnrollmentRefundProcessor
    {
        public Task<Result<EnrollmentRefundExecutionResult>> ExecuteAsync(EnrollmentCancellationSnapshot snapshot, EnrollmentRefundCalculation calculation, string idempotencyKey, long actorUserAccountId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<EnrollmentRefundExecutionResult>.Success(new(1, "SUCCEEDED", calculation.RefundAmount, calculation.EducationAccountRefundAmount, calculation.OnlineRefundAmount)));
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 99;
        public long? PersonId => 1;
        public long? OrganizationUnitId => 10;
        public IReadOnlyCollection<long> OrganizationUnitIds => [10];
        public IReadOnlyCollection<string> Roles => ["STUDENT"];
        public IReadOnlyCollection<string> Permissions => [];
        public string Portal => "ESERVICE";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => false;
    }

    private sealed class DisabledMailSwitch : IEmailDeliverySwitch
    {
        public bool IsEnabled => false;
    }

    private sealed class NullRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(long personId, CancellationToken cancellationToken) => Task.FromResult<EmailRecipient?>(null);
        public EmailRecipient? ResolveProvided(string? emailAddress) => null;
    }

    private sealed class NoopEmailGateway : IEmailDeliveryGateway
    {
        public Task<Result> SendAsync(EmailDeliveryMessage message, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
