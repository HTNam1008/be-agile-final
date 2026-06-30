using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.IGateway.People;
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
        FakeEmailDeliveryGateway mailGateway = new();
        FakeOutstandingReader outstandingReader = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        SeedAccountHolder(dbContext, personId: 2, accountId: 102, dateOfBirth: Today.AddMonths(3).AddYears(-30));
        SeedAccountHolder(dbContext, personId: 3, accountId: 103, dateOfBirth: Today.AddDays(7).AddYears(-30));
        outstandingReader.AmountByPersonId[2] = 42.50m;
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailGateway, outstandingReader);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailGateway.Messages.Should().HaveCount(3);
        mailGateway.Messages.Should().Contain(message =>
            message.PlainTextBody.Contains("6 months", StringComparison.Ordinal)
            && message.PlainTextBody.Contains("15 Jul 2026", StringComparison.Ordinal));
        mailGateway.Messages.Should().Contain(message =>
            message.PlainTextBody.Contains("3 months", StringComparison.Ordinal)
            && message.PlainTextBody.Contains("15 Apr 2026", StringComparison.Ordinal)
            && message.PlainTextBody.Contains("Outstanding Amount: SGD 42.50", StringComparison.Ordinal));
        mailGateway.Messages.Should().Contain(message =>
            message.PlainTextBody.Contains("1 week", StringComparison.Ordinal)
            && message.PlainTextBody.Contains("22 Jan 2026", StringComparison.Ordinal));
        mailGateway.Messages.Should().OnlyContain(message =>
            message.Subject == "Reminder: Your MOE SEEDS account will be locked soon"
            && message.ToEmail == "student.real@example.com"
            && message.PlainTextBody.Contains("Go to Payment Dashboard", StringComparison.Ordinal)
            && message.HtmlBody != null
            && message.HtmlBody.Contains("#DC343B", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendDueRemindersAsync_Does_Not_Send_When_No_Milestone_Matches()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddDays(30).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailGateway);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueRemindersAsync_Does_Not_Fail_When_Recipient_Is_Missing()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(
            dbContext,
            mailGateway,
            recipientResolver: new TestDoubles.FixedEmailRecipientResolver(null));

        Func<Task> act = () => service.SendDueRemindersAsync(Today, CancellationToken.None);

        await act.Should().NotThrowAsync();
        mailGateway.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task SendDueRemindersAsync_Does_Not_Fail_When_Mail_Gateway_Fails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new()
        {
            ResultToReturn = Result.Failure(new Error("MAIL.TEST_FAILURE", "Mail failed."))
        };
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailGateway);

        Func<Task> act = () => service.SendDueRemindersAsync(Today, CancellationToken.None);

        await act.Should().NotThrowAsync();
        mailGateway.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task SendDueRemindersAsync_Uses_Generic_Text_When_Outstanding_Is_Missing()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(dbContext, mailGateway);

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        EmailDeliveryMessage message = mailGateway.Messages.Should().ContainSingle().Which;
        message.PlainTextBody.Should().NotContain("Outstanding Amount:");
        message.PlainTextBody.Should().Contain("Please review and settle any outstanding charges before your account is locked.");
    }

    [Fact]
    public async Task SendDueRemindersAsync_WhenMailDeliveryDisabled_Does_Not_Call_RecipientResolver_Or_Gateway()
    {
        using MoeDbContext dbContext = CreateDbContext();
        FakeEmailDeliveryGateway mailGateway = new();
        SeedAccountHolder(dbContext, personId: 1, accountId: 101, dateOfBirth: Today.AddMonths(6).AddYears(-30));
        await dbContext.SaveChangesAsync();
        Age30AccountLockReminderEmailService service = CreateService(
            dbContext,
            mailGateway,
            new ThrowingOutstandingReader(),
            new ThrowingEmailRecipientResolver(),
            new TestDoubles.FixedEmailDeliverySwitch(isEnabled: false));

        await service.SendDueRemindersAsync(Today, CancellationToken.None);

        mailGateway.Messages.Should().BeEmpty();
    }

    private static Age30AccountLockReminderEmailService CreateService(
        MoeDbContext dbContext,
        FakeEmailDeliveryGateway mailGateway,
        FakeOutstandingReader? outstandingReader = null,
        IEmailRecipientResolver? recipientResolver = null,
        IEmailDeliverySwitch? mailSwitch = null)
        => new(
            dbContext,
            recipientResolver ?? new TestDoubles.FixedEmailRecipientResolver(),
            mailGateway,
            mailSwitch ?? new TestDoubles.FixedEmailDeliverySwitch(),
            outstandingReader ?? new FakeOutstandingReader(),
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

    private sealed class ThrowingEmailRecipientResolver : IEmailRecipientResolver
    {
        public Task<EmailRecipient?> ResolveForPersonAsync(
            long personId,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");

        public EmailRecipient? ResolveProvided(string? providedEmail)
            => throw new InvalidOperationException("Recipient resolver should not be called when mail is disabled.");
    }
}
