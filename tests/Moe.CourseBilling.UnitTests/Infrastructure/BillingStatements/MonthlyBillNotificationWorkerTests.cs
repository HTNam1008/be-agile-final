using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.CourseBilling;
using Moe.Modules.CourseBilling.Contracts.BillingStatements;
using Moe.Modules.CourseBilling.Domain.Billing;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.CourseBilling.IGateway.Repositories;
using Moe.Modules.CourseBilling.Infrastructure.BillingStatements;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.BillingStatements;

public sealed class MonthlyBillNotificationWorkerTests
{
    [Fact]
    public async Task RunIfDueAsync_WhenNotFirstDay_DoesNotProcessOutstandingBills()
    {
        DbContextOptions<MoeDbContext> options = CreateOptions();
        await SeedBillAsync(options, personId: 700, outstandingAmount: 25m);
        RecordingBillingStatementRepository statements = new();
        MonthlyBillNotificationWorker worker = CreateWorker(
            options,
            statements,
            new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero));

        await worker.RunIfDueAsync(CancellationToken.None);

        statements.PersonIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RunIfDueAsync_OnFirstDay_ProcessesOutstandingBillsOnce()
    {
        DbContextOptions<MoeDbContext> options = CreateOptions();

        await SeedBillAsync(options, personId: 701, outstandingAmount: 25m);
        await SeedBillAsync(options, personId: 702, outstandingAmount: 0m);

        RecordingBillingStatementRepository statements = new();
        MonthlyBillNotificationWorker worker = CreateWorker(
            options,
            statements,
            new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));

        await worker.RunIfDueAsync(CancellationToken.None);
        await worker.RunIfDueAsync(CancellationToken.None);

        statements.PersonIds.Should().Equal(701);
        statements.NotificationModes.Should().Equal(BillingStatementNotificationMode.SendMonthlyBill);
    }

    [Fact]
    public async Task RunIfDueAsync_DeduplicatesPersonWithMultipleOutstandingBills()
    {
        DbContextOptions<MoeDbContext> options = CreateOptions();
        await SeedBillAsync(options, personId: 703, outstandingAmount: 10m, billNumberSuffix: "A");
        await SeedBillAsync(options, personId: 703, outstandingAmount: 20m, billNumberSuffix: "B");
        RecordingBillingStatementRepository statements = new();
        MonthlyBillNotificationWorker worker = CreateWorker(
            options,
            statements,
            new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));

        await worker.RunIfDueAsync(CancellationToken.None);

        statements.PersonIds.Should().Equal(703);
        statements.NotificationModes.Should().Equal(BillingStatementNotificationMode.SendMonthlyBill);
    }

    [Fact]
    public async Task RunIfDueAsync_FiltersOtherMonthAndPendingPlanSelection()
    {
        DbContextOptions<MoeDbContext> options = CreateOptions();
        await SeedBillAsync(
            options,
            personId: 704,
            outstandingAmount: 10m,
            dueDate: new DateOnly(2026, 8, 1));
        await SeedBillAsync(
            options,
            personId: 705,
            outstandingAmount: 10m,
            pendingPlanSelection: true);
        RecordingBillingStatementRepository statements = new();
        MonthlyBillNotificationWorker worker = CreateWorker(
            options,
            statements,
            new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));

        await worker.RunIfDueAsync(CancellationToken.None);

        statements.PersonIds.Should().BeEmpty();
    }

    private static DbContextOptions<MoeDbContext> CreateOptions()
        => new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"monthly-bill-worker-{Guid.NewGuid():N}")
            .Options;

    private static MonthlyBillNotificationWorker CreateWorker(
        DbContextOptions<MoeDbContext> options,
        RecordingBillingStatementRepository statements,
        DateTimeOffset utcNow)
    {
        ServiceCollection services = new();
        services.AddScoped(_ => new MoeDbContext(options, [new CourseBillingModelConfiguration()]));
        services.AddSingleton<IBillingStatementRepository>(statements);
        ServiceProvider provider = services.BuildServiceProvider();

        return new MonthlyBillNotificationWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(utcNow),
            NullLogger<MonthlyBillNotificationWorker>.Instance);
    }

    private static async Task SeedBillAsync(
        DbContextOptions<MoeDbContext> options,
        long personId,
        decimal outstandingAmount,
        string billNumberSuffix = "A",
        DateOnly? dueDate = null,
        bool pendingPlanSelection = false)
    {
        await using MoeDbContext dbContext = new(options, [new CourseBillingModelConfiguration()]);
        await dbContext.Database.EnsureCreatedAsync();

        Course course = new(
            organizationId: 10,
            courseCode: $"MONTHLY-{personId}",
            courseName: $"Monthly Course {personId}",
            description: null,
            startDate: new DateOnly(2026, 7, 10),
            endDate: new DateOnly(2026, 8, 10),
            enrollmentOpenAtUtc: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            enrollmentCloseAtUtc: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            actorLoginAccountId: 1,
            utcNow: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        dbContext.Add(course);
        await dbContext.SaveChangesAsync();

        CourseEnrollment enrollment = pendingPlanSelection
            ? CourseEnrollment.EnrollByAdminPendingPlanSelection(
                personId,
                course.Id,
                adminLoginAccountId: 1,
                enrolledAtUtc: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                beforeStartRefundPercentage: CourseRefundPolicyDefaults.BeforeStartPercentage,
                afterStartRefundPercentage: CourseRefundPolicyDefaults.AfterStartPercentage).Value
            : CourseEnrollment.EnrollByAdmin(
                personId,
                course.Id,
                coursePaymentPlanId: personId,
                adminLoginAccountId: 1,
                enrolledAtUtc: new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                beforeStartRefundPercentage: CourseRefundPolicyDefaults.BeforeStartPercentage,
                afterStartRefundPercentage: CourseRefundPolicyDefaults.AfterStartPercentage).Value;
        dbContext.Add(enrollment);
        await dbContext.SaveChangesAsync();

        Bill bill = Bill.IssueForCourseEnrollment(
            enrollment.Id,
            $"MONTHLY-BILL-{personId}-{billNumberSuffix}",
            new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            dueDate ?? new DateOnly(2026, 7, 15),
            outstandingAmount).Value;
        dbContext.Add(bill);
        await dbContext.SaveChangesAsync();
    }

    private sealed class RecordingBillingStatementRepository : IBillingStatementRepository
    {
        public List<long> PersonIds { get; } = [];
        public List<BillingStatementNotificationMode> NotificationModes { get; } = [];

        public Task<BillingStatementResponse> GetOrCreateAsync(
            long personId,
            int year,
            int month,
            DateTime utcNow,
            BillingStatementNotificationMode notificationMode,
            CancellationToken cancellationToken)
        {
            PersonIds.Add(personId);
            NotificationModes.Add(notificationMode);
            return Task.FromResult<BillingStatementResponse>(null!);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
