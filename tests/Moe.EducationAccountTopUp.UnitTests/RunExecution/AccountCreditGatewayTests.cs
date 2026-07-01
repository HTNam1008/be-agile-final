using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class AccountCreditGatewayTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 18, 3, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task Should_Credit_Account_And_Return_TransactionId()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5001, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(dbContext, mailQueue);

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            50m,
            "topup:1:5001",
            "Test top-up");

        result.IsSuccess.Should().BeTrue();
        result.Value.AlreadyProcessed.Should().BeFalse();
        result.Value.AccountTransactionId.Should().BeGreaterThan(0);

        AccountTransaction transaction = await dbContext.Set<AccountTransaction>().SingleAsync();
        transaction.Id.Should().Be(result.Value.AccountTransactionId);
        transaction.TransactionTypeCode.Should().Be("CREDIT");
        transaction.Amount.Should().Be(50m);
        transaction.BalanceAfter.Should().Be(150m);

        mailQueue.Jobs.Should().ContainSingle();
        EmailNotificationJob job = mailQueue.Jobs.Single();
        job.NotificationType.Should().Be("NOTI-02");
        job.PersonId.Should().Be(5001);
        job.Subject.Should().Be("Funds Credited to Your Education Account");
        job.PlainTextBody.Should().Contain("SGD 50.00 has been credited to your Education Account.");
        job.PlainTextBody.Should().Contain("Reason/Campaign: Test top-up");
        job.PlainTextBody.Should().Contain("Updated Balance: SGD 150.00");
        job.HtmlBody.Should().Contain("View My Account");
    }

    [Fact]
    public async Task Should_Return_Existing_When_Already_Processed()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5002, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(dbContext);

        var first = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            50m,
            "topup:2:5002",
            "First request");

        var second = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            50m,
            "topup:2:5002",
            "Replay request");

        second.IsSuccess.Should().BeTrue();
        second.Value.AlreadyProcessed.Should().BeTrue();
        second.Value.AccountTransactionId.Should().Be(first.Value.AccountTransactionId);
        dbContext.Set<AccountTransaction>().Count().Should().Be(1);
        account.CachedBalance.Should().Be(150m);
    }

    [Fact]
    public async Task Should_Fail_When_Account_Not_Found()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AccountCreditGateway gateway = CreateGateway(dbContext);

        var result = await gateway.CreditAccountForTopUpAsync(
            999_999,
            50m,
            "topup:3:999999",
            "Missing account");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.AccountNotFound);
    }

    [Fact]
    public async Task Should_Fail_When_Account_Not_Active()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5004, balance: 100m);
        account.CloseManual(_clock.UtcNow, EducationAccountClosingReasonCodes.Other, "Test closure", 1);
        await dbContext.SaveChangesAsync();
        AccountCreditGateway gateway = CreateGateway(dbContext);

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            50m,
            "topup:4:5004",
            "Inactive account");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.AccountNotActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_Fail_When_Amount_Not_Positive(decimal amount)
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5005, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(dbContext);

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            amount,
            $"topup:5:5005:{amount}",
            "Invalid amount");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TopUpErrors.InvalidCreditAmount);
    }

    [Fact]
    public async Task Should_Update_Balance_Correctly()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5006, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(dbContext);

        await gateway.CreditAccountForTopUpAsync(
            account.Id,
            50m,
            "topup:6:5006",
            "Balance update");

        account.CachedBalance.Should().Be(150m);
        (await dbContext.Set<EducationAccount>().FindAsync(account.Id))!
            .CachedBalance.Should().Be(150m);
    }

    [Fact]
    public async Task Should_Handle_Concurrent_Duplicate_Key()
    {
        using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5007, balance: 100m);
        AccountTransaction existingTransaction = AccountTransaction.Create(
            account.Id,
            "CREDIT",
            25m,
            "TOPUP",
            null,
            "topup:7:5007",
            account.CachedBalance,
            "Existing transaction",
            null,
            _clock.UtcNow.UtcDateTime);

        dbContext.Set<AccountTransaction>().Add(existingTransaction);
        account.UpdateBalance(25m);
        await dbContext.SaveChangesAsync();

        AccountCreditGateway gateway = CreateGateway(dbContext);

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            25m,
            "topup:7:5007",
            "Duplicate request");

        result.IsSuccess.Should().BeTrue();
        result.Value.AlreadyProcessed.Should().BeTrue();
        result.Value.AccountTransactionId.Should().Be(existingTransaction.Id);
        dbContext.Set<AccountTransaction>().Count().Should().Be(1);
        account.CachedBalance.Should().Be(125m);
    }

    [Fact]
    public async Task Should_Not_Fail_Credit_When_Email_Queue_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5008, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(dbContext, mailQueue);

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            25m,
            "topup:8:5008",
            "Government campaign");

        result.IsSuccess.Should().BeTrue();
        account.CachedBalance.Should().Be(125m);
        mailQueue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task Should_Credit_Account_Without_Email_When_MailDelivery_Disabled()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        EducationAccount account = await AddAccountAsync(dbContext, personId: 5009, balance: 100m);
        AccountCreditGateway gateway = CreateGateway(
            dbContext,
            mailQueue,
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        var result = await gateway.CreditAccountForTopUpAsync(
            account.Id,
            25m,
            "topup:9:5009",
            "Government campaign");

        result.IsSuccess.Should().BeTrue();
        account.CachedBalance.Should().Be(125m);
        mailQueue.Jobs.Should().BeEmpty();
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, new[] { new TestModelConfigurationContributor() });
    }

    private async Task<EducationAccount> AddAccountAsync(
        MoeDbContext dbContext,
        long personId,
        decimal balance)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-TEST-{personId}",
            _clock.UtcNow,
            "TEST",
            "Test account",
            openedBy: 1).Value;

        account.UpdateBalance(balance);
        dbContext.Set<EducationAccount>().Add(account);
        Person person = new(
            personId,
            $"EXT-{personId}",
            $"Student {personId}",
            new DateOnly(2000, 1, 1),
            "SG",
            null);
        person.UpdatePreferredContact("student.real@example.com", null, null, _clock.UtcNow.UtcDateTime);
        dbContext.Set<Person>().Add(person);
        await dbContext.SaveChangesAsync();

        return account;
    }

    private AccountCreditGateway CreateGateway(
        MoeDbContext dbContext,
        IEmailNotificationQueue? mailQueue = null,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            _clock,
            new FakeTopUpExecutionMetrics(),
            mailQueue ?? new TestDoubles.RecordingEmailNotificationQueue(),
            mailSwitch ?? new TestDoubles.FixedEmailDeliverySwitch(),
            NullLogger<AccountCreditGateway>.Instance);

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<EducationAccount>().HasKey(x => x.Id);
            modelBuilder.Entity<SchoolEnrollment>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaign>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaignRule>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpRun>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpTransaction>().HasKey(x => x.Id);
            modelBuilder.Entity<AccountTransaction>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaignRecipient>().HasKey(x => x.Id);
        }
    }

    private sealed class FakeTopUpExecutionMetrics : ITopUpExecutionMetrics
    {
        public void RecordRunStarted(long topUpRunId, long campaignId, int totalSelected) { }

        public void RecordRunCompleted(
            long topUpRunId,
            long campaignId,
            string terminalStatus,
            int totalProcessed,
            int totalSucceeded,
            int totalFailed,
            int totalSkipped,
            TimeSpan duration)
        { }

        public void RecordRecipientProcessed(
            long topUpRunId,
            string status,
            bool duplicateIdempotencyHit,
            bool accountCreditFailure)
        { }

        public void RecordAccountCreditDbConflict() { }
    }

}
