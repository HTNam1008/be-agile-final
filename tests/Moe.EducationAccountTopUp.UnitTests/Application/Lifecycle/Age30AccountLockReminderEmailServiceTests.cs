using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.Lifecycle;

public sealed class Age30AccountLockReminderEmailServiceTests
{
    private static readonly DateOnly Today = new(2026, 1, 15);
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendDueRemindersAsync_Sends_For_6_Month_3_Month_And_1_Week_Milestones()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        FakeOutstandingReader outstandingReader = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        SeedAccountHolder(dbContext, personId: 2, accountId: 102, dateOfBirth: Today.AddMonths(3).AddYears(-30));
        SeedAccountHolder(dbContext, personId: 3, accountId: 103, dateOfBirth: Today.AddDays(7).AddYears(-30));
        outstandingReader.AmountByPersonId[2] = 42.50m;
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailQueue, outstandingReader);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailQueue.Jobs.Should().HaveCount(3);
        mailQueue.Jobs.Should().Contain(job =>
            job.PersonId == 1
            && job.PlainTextBody.Contains("6 months", StringComparison.Ordinal)
            && job.PlainTextBody.Contains("15 Jul 2026", StringComparison.Ordinal));
        mailQueue.Jobs.Should().Contain(job =>
            job.PersonId == 2
            && job.PlainTextBody.Contains("3 months", StringComparison.Ordinal)
            && job.PlainTextBody.Contains("15 Apr 2026", StringComparison.Ordinal)
            && job.PlainTextBody.Contains("Outstanding Amount: SGD 42.50", StringComparison.Ordinal));
        mailQueue.Jobs.Should().Contain(job =>
            job.PersonId == 3
            && job.PlainTextBody.Contains("1 week", StringComparison.Ordinal)
            && job.PlainTextBody.Contains("22 Jan 2026", StringComparison.Ordinal));
        mailQueue.Jobs.Should().OnlyContain(job =>
            job.NotificationType == "AGE-30-LOCK-REMINDER"
            && job.Subject == "Reminder: Your Ministry of Education - Singapore account will be locked soon"
            && job.PlainTextBody.Contains("Go to Payment Dashboard", StringComparison.Ordinal)
            && job.HtmlBody != null
            && job.HtmlBody.Contains("#DC343B", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendDueRemindersAsync_Does_Not_Send_When_No_Milestone_Matches()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddDays(30).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailQueue);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailQueue.Jobs.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueRemindersAsync_Does_Not_Fail_When_Email_Queue_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new()
        {
            Result = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailQueue);

        Func<Task> act = () => service.SendDueRemindersAsync(Today, CancellationToken.None);

        await act.Should().NotThrowAsync();
        mailQueue.Jobs.Should().ContainSingle();
    }

    [Fact]
    public async Task SendDueRemindersAsync_Uses_Generic_Text_When_Outstanding_Is_Missing()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailQueue);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        EmailNotificationJob job = mailQueue.Jobs.Should().ContainSingle().Which;
        job.PlainTextBody.Should().NotContain("Outstanding Amount:");
        job.PlainTextBody.Should().Contain("Please review and settle any outstanding charges before your account is locked.");
    }

    [Fact]
    public async Task SendDueRemindersAsync_WhenMailDeliveryDisabled_Does_Not_Call_OutstandingReader_Or_Queue()
    {
        using MoeDbContext dbContext = CreateDbContext();
        TestDoubles.RecordingEmailNotificationQueue mailQueue = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(
            dbContext,
            mailQueue,
            new ThrowingOutstandingReader(),
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailQueue.Jobs.Should().BeEmpty();
    }

    private static Age30AccountLockReminderEmailService CreateService(
        MoeDbContext dbContext,
        IEmailNotificationQueue mailQueue,
        FakeOutstandingReader? outstandingReader = null,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            new TestDoubles.RecordingEmailNotificationScheduler(mailQueue, mailSwitch),
            outstandingReader ?? new FakeOutstandingReader(),
            new TestDoubles.FixedEmailBrandingProvider(),
            NullLogger<Age30AccountLockReminderEmailService>.Instance);

    private static void SeedAccountHolder(
        MoeDbContext dbContext,
        long personId,
        long accountId,
        DateOnly dateOfBirth)
    {
        Person person = new(
            personId,
            $"EXT-{personId}",
            $"Student {personId}",
            dateOfBirth,
            "SG",
            "CITIZEN");

        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-{accountId}",
            Now,
            "EXCEPTION",
            "Manual account",
            99).Value;
        SetId(account, accountId);

        dbContext.Set<Person>().Add(person);
        dbContext.Set<EducationAccount>().Add(account);
    }

    private static void SetId(EducationAccount account, long id)
        => typeof(EducationAccount)
            .GetProperty(nameof(EducationAccount.Id), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(account, id);

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfiguration()]);
    }

    private sealed class TestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            new EducationAccountTopUpModelConfiguration().Configure(modelBuilder);
        }
    }

    private class FakeOutstandingReader : IAccountLockReminderOutstandingReader
    {
        public Dictionary<long, decimal?> AmountByPersonId { get; } = [];

        public virtual Task<decimal?> FindOutstandingAmountAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(AmountByPersonId.GetValueOrDefault(personId));
    }

    private sealed class ThrowingOutstandingReader : FakeOutstandingReader
    {
        public override Task<decimal?> FindOutstandingAmountAsync(long personId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Outstanding reader should not be called when mail is disabled.");
    }

}
