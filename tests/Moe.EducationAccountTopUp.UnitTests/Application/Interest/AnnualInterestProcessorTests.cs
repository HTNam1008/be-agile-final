using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.Interest;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.Interest;

public sealed class AnnualInterestProcessorTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2027, 1, 1, 18, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProcessDueInterestAsync_CreditsActivePositiveBalanceOnce()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5001, balance: 1000m);
        await dbContext.SaveChangesAsync();
        AnnualInterestProcessor processor = CreateProcessor(dbContext);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 1),
            ProcessedAtUtc);

        result.ProcessedCount.Should().Be(1);
        result.TotalInterestAmount.Should().Be(20m);
        account.CachedBalance.Should().Be(1020m);

        AccountTransaction transaction = await dbContext.Set<AccountTransaction>().SingleAsync();
        transaction.TransactionTypeCode.Should().Be(EducationAccountInterestCodes.TransactionTypeCode);
        transaction.ReferenceTypeCode.Should().Be(EducationAccountInterestCodes.ReferenceTypeCode);
        transaction.ReferenceId.Should().Be(2026);
        transaction.Amount.Should().Be(20m);
        transaction.BalanceAfter.Should().Be(1020m);
        transaction.IdempotencyKey.Should().Be($"interest:2026:{account.Id}");
    }

    [Fact]
    public async Task ProcessDueInterestAsync_SendsInAppNotificationWhenInterestIsCredited()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5010, balance: 1000m);
        await dbContext.SaveChangesAsync();
        FakeStudentNotificationRecipientResolver recipients = new() { UserAccountId = 9001 };
        FakeNotificationWriter notifications = new();
        AnnualInterestProcessor processor = CreateProcessor(dbContext, recipients, notifications);

        await processor.ProcessDueInterestAsync(new DateOnly(2027, 1, 1), ProcessedAtUtc);

        notifications.Requests.Should().ContainSingle();
        NotificationCreateRequest request = notifications.Requests.Single();
        request.RecipientUserAccountId.Should().Be(9001);
        request.NotificationTypeCode.Should().Be(NotificationTypeCode.AccInterestCredited);
        request.Title.Should().Be("Annual Interest Credited");
        request.Body.Should().Contain("SGD 20.00");
        request.Body.Should().Contain(account.AccountNumber);
        request.Body.Should().Contain("2026");
    }

    [Fact]
    public async Task ProcessDueInterestAsync_DoesNotFailWhenNoNotificationRecipientExists()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5011, balance: 1000m);
        await dbContext.SaveChangesAsync();
        FakeStudentNotificationRecipientResolver recipients = new() { UserAccountId = null };
        FakeNotificationWriter notifications = new();
        AnnualInterestProcessor processor = CreateProcessor(dbContext, recipients, notifications);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 1),
            ProcessedAtUtc);

        result.ProcessedCount.Should().Be(1);
        account.CachedBalance.Should().Be(1020m);
        notifications.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDueInterestAsync_DoesNotSendDuplicateNotificationForAlreadyCreditedYear()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5012, balance: 1000m);
        await dbContext.SaveChangesAsync();
        FakeStudentNotificationRecipientResolver recipients = new() { UserAccountId = 9002 };
        FakeNotificationWriter notifications = new();
        AnnualInterestProcessor processor = CreateProcessor(dbContext, recipients, notifications);

        await processor.ProcessDueInterestAsync(new DateOnly(2027, 1, 1), ProcessedAtUtc);
        AnnualInterestProcessingResult second = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 2),
            ProcessedAtUtc.AddDays(1));

        second.ProcessedCount.Should().Be(0);
        dbContext.Set<AccountTransaction>().Count().Should().Be(1);
        notifications.Requests.Should().ContainSingle();
        account.CachedBalance.Should().Be(1020m);
    }

    [Fact]
    public async Task ProcessDueInterestAsync_KeepsInterestCreditWhenNotificationCreationFails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5013, balance: 1000m);
        await dbContext.SaveChangesAsync();
        FakeStudentNotificationRecipientResolver recipients = new() { UserAccountId = 9003 };
        FakeNotificationWriter notifications = new() { ShouldFail = true };
        AnnualInterestProcessor processor = CreateProcessor(dbContext, recipients, notifications);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 1),
            ProcessedAtUtc);

        result.ProcessedCount.Should().Be(1);
        account.CachedBalance.Should().Be(1020m);
        AccountTransaction transaction = await dbContext.Set<AccountTransaction>().SingleAsync();
        transaction.TransactionTypeCode.Should().Be(EducationAccountInterestCodes.TransactionTypeCode);
        notifications.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ProcessDueInterestAsync_IsIdempotentForSameAccountAndYear()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5002, balance: 1000m);
        await dbContext.SaveChangesAsync();
        AnnualInterestProcessor processor = CreateProcessor(dbContext);

        await processor.ProcessDueInterestAsync(new DateOnly(2027, 1, 1), ProcessedAtUtc);
        AnnualInterestProcessingResult second = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 2),
            ProcessedAtUtc.AddDays(1));

        second.ProcessedCount.Should().Be(0);
        dbContext.Set<AccountTransaction>().Count().Should().Be(1);
        account.CachedBalance.Should().Be(1020m);
    }

    [Fact]
    public async Task ProcessDueInterestAsync_SkipsZeroAndClosedAccounts()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AddAccount(dbContext, personId: 5003, balance: 0m);
        EducationAccount closed = AddAccount(dbContext, personId: 5004, balance: 1000m);
        closed.CloseManual(ProcessedAtUtc, EducationAccountClosingReasonCodes.Other, "Test", 1);
        await dbContext.SaveChangesAsync();
        AnnualInterestProcessor processor = CreateProcessor(dbContext);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 1, 1),
            ProcessedAtUtc);

        result.ProcessedCount.Should().Be(0);
        dbContext.Set<AccountTransaction>().Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDueInterestAsync_UsesYearEndSnapshotForDelayedRuns()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = AddAccount(dbContext, personId: 5005, balance: 1500m);
        await dbContext.SaveChangesAsync();
        dbContext.Set<AccountTransaction>().Add(AccountTransaction.Create(
            account.Id,
            "CREDIT",
            1000m,
            "TOPUP",
            null,
            "topup:before-year-close",
            0m,
            "Before year close",
            null,
            new DateTime(2026, 12, 31, 12, 0, 0, DateTimeKind.Utc)));
        dbContext.Set<AccountTransaction>().Add(AccountTransaction.Create(
            account.Id,
            "CREDIT",
            500m,
            "TOPUP",
            null,
            "topup:after-year-close",
            1000m,
            "After year close",
            null,
            new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc)));
        await dbContext.SaveChangesAsync();
        AnnualInterestProcessor processor = CreateProcessor(dbContext);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2027, 2, 1),
            ProcessedAtUtc.AddMonths(1));

        result.TotalInterestAmount.Should().Be(20m);
        AccountTransaction interest = await dbContext.Set<AccountTransaction>()
            .SingleAsync(x => x.TransactionTypeCode == EducationAccountInterestCodes.TransactionTypeCode);
        interest.Amount.Should().Be(20m);
        interest.BalanceAfter.Should().Be(1520m);
    }

    [Fact]
    public async Task ProcessDueInterestAsync_DoesNotBackCreditBeforeConfiguredFirstYear()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AddAccount(dbContext, personId: 5006, balance: 1000m);
        await dbContext.SaveChangesAsync();
        AnnualInterestProcessor processor = CreateProcessor(dbContext);

        AnnualInterestProcessingResult result = await processor.ProcessDueInterestAsync(
            new DateOnly(2026, 12, 31),
            ProcessedAtUtc);

        result.ProcessedCount.Should().Be(0);
        dbContext.Set<AccountTransaction>().Should().BeEmpty();
    }

    private static AnnualInterestProcessor CreateProcessor(
        MoeDbContext dbContext,
        IStudentNotificationRecipientResolver? notificationRecipients = null,
        INotificationWriter? notificationWriter = null)
        => new(
            dbContext,
            Options.Create(new EducationAccountInterestOptions
            {
                AnnualRate = 0.02m,
                FirstInterestYear = 2026
            }),
            NullLogger<AnnualInterestProcessor>.Instance,
            notificationRecipients ?? new FakeStudentNotificationRecipientResolver(),
            notificationWriter ?? new FakeNotificationWriter());

    private static EducationAccount AddAccount(MoeDbContext dbContext, long personId, decimal balance)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-{personId}",
            new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            "TEST",
            "Test account",
            99).Value;
        account.UpdateBalance(balance);
        dbContext.Set<EducationAccount>().Add(account);
        return account;
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new EducationAccountConfiguration());
            modelBuilder.ApplyConfiguration(new AccountTransactionConfiguration());
        }
    }

    private sealed class FakeStudentNotificationRecipientResolver : IStudentNotificationRecipientResolver
    {
        public long? UserAccountId { get; init; } = 7001;

        public Task<long?> FindUserAccountIdByPersonIdAsync(
            long personId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(UserAccountId);
    }

    private sealed class FakeNotificationWriter : INotificationWriter
    {
        public List<NotificationCreateRequest> Requests { get; } = [];
        public bool ShouldFail { get; init; }

        public Task<Result<long>> CreateAsync(
            NotificationCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ShouldFail
                ? Task.FromResult(Result<long>.Failure(new Error("notification.failed", "Notification creation failed.")))
                : Task.FromResult(Result<long>.Success(Requests.Count));
        }
    }
}
