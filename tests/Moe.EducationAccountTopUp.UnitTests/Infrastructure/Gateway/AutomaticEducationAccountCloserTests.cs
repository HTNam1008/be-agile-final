using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Application.CloseAccount;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateway;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.People;
using Moe.Modules.MailDelivery.IGateway;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Infrastructure.Gateway;

public sealed class AutomaticEducationAccountCloserTests
{
    private static readonly DateOnly Today = new(2026, 6, 24);
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeEducationAccountRepository _educationAccounts = new();
    private readonly FakeEligiblePersonLookupGateway _people = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakePersonDirectory _personDirectory = new();
    private readonly FakeEmailDeliveryGateway _mailGateway = new();

    [Fact]
    public async Task CloseEligibleAsync_ClosesActiveAccountsForPeopleAgedAtLeast30_RegardlessOfOpeningMode()
    {
        EducationAccount manual = AddManualAccount(1001, personId: 2001);
        EducationAccount automatic = AddAutomaticAccount(1002, personId: 2002);
        EducationAccount singpass = AddAutomaticAccount(1003, personId: 2003);
        EducationAccount underAge = AddManualAccount(1004, personId: 2004);
        manual.UpdateBalance(72.25m);
        automatic.UpdateBalance(15m);
        singpass.UpdateBalance(30m);
        _people.AgedAtLeastPersonIds = [2001, 2002, 2003];
        AutomaticEducationAccountCloser closer = CreateCloser();

        AutomaticEducationAccountClosureSummary result =
            await closer.CloseEligibleAsync(Today, Now, CancellationToken.None);

        result.ActiveAccountCount.Should().Be(4);
        result.ClosedCount.Should().Be(3);
        manual.StatusCode.Should().Be(AccountStatuses.Closed);
        automatic.StatusCode.Should().Be(AccountStatuses.Closed);
        singpass.StatusCode.Should().Be(AccountStatuses.Closed);
        underAge.StatusCode.Should().Be(AccountStatuses.Active);
        manual.CachedBalance.Should().Be(72.25m);
        automatic.CachedBalance.Should().Be(15m);
        singpass.CachedBalance.Should().Be(30m);
        _people.RequestedPersonIds.Should().BeEquivalentTo([2001L, 2002L, 2003L, 2004L]);
        _people.RequestedMinAge.Should().Be(30);
        _people.RequestedToday.Should().Be(Today);
        _audit.Calls.Should().HaveCount(3);
        _audit.Calls.Select(x => x.ActionCode).Should()
            .OnlyContain(x => x == AuditActionCodes.EducationAccountClosedAutomatically);
        _unitOfWork.SaveCalls.Should().Be(3);
        _mailGateway.Messages.Should().HaveCount(3);
    }

    [Fact]
    public async Task EnsureClosedAsync_WhenAlreadyClosed_IsNoOpAndDoesNotAddDuplicateAudit()
    {
        EducationAccount account = AddAutomaticAccount(2001, personId: 3001);
        AutomaticEducationAccountCloser closer = CreateCloser();
        await closer.EnsureClosedAsync(account, Now, CancellationToken.None);

        AutomaticEducationAccountClosureResult secondResult =
            await closer.EnsureClosedAsync(account, Now.AddDays(1), CancellationToken.None);

        secondResult.Closed.Should().BeFalse();
        _audit.Calls.Should().ContainSingle();
        _unitOfWork.SaveCalls.Should().Be(1);
        account.ClosedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task EnsureClosedAsync_WritesAutoAgeLimitAuditDetails()
    {
        EducationAccount account = AddManualAccount(3001, personId: 4001);
        AutomaticEducationAccountCloser closer = CreateCloser();

        await closer.EnsureClosedAsync(account, Now, CancellationToken.None);

        AuditCall call = _audit.Calls.Single();
        call.EntityTypeCode.Should().Be("EducationAccount");
        call.EntityId.Should().Be(account.Id.ToString());
        using JsonDocument document = JsonDocument.Parse(call.DetailsJson!);
        JsonElement root = document.RootElement;
        root.GetProperty("personId").GetInt64().Should().Be(4001);
        root.GetProperty("reasonCode").GetString().Should().Be(EducationAccountClosingReasonCodes.AutoAgeLimit);
        root.GetProperty("closedByLoginAccountId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task EnsureClosedAsync_DoesNotCreateTransactions()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        EducationAccount account = EducationAccount.OpenAutomatically(
            5001,
            "PSEA-00005001",
            Now.AddYears(-1)).Value;
        SetId(account, 5001);
        account.UpdateBalance(98.75m);
        dbContext.Set<EducationAccount>().Add(account);
        await dbContext.SaveChangesAsync();
        AutomaticEducationAccountCloser closer = new(
            new EducationAccountRepository(dbContext),
            _people,
            _audit,
            new DbUnitOfWork(dbContext),
            CreateClosureEmails());

        await closer.EnsureClosedAsync(account, Now, CancellationToken.None);

        dbContext.Set<AccountTransaction>().Should().BeEmpty();
        account.CachedBalance.Should().Be(98.75m);
    }

    private AutomaticEducationAccountCloser CreateCloser()
        => new(_educationAccounts, _people, _audit, _unitOfWork, CreateClosureEmails());

    private EducationAccountClosureEmailService CreateClosureEmails()
        => new(
            _personDirectory,
            new TestDoubles.FixedEmailRecipientResolver(),
            _mailGateway,
            new TestDoubles.FixedEmailDeliverySwitch(),
            NullLogger<EducationAccountClosureEmailService>.Instance);

    private EducationAccount AddManualAccount(long accountId, long personId)
    {
        EducationAccount account = EducationAccount.OpenManual(
            personId,
            $"EA-{accountId}",
            Now.AddYears(-1),
            "MANUAL_TEST",
            "Manual account",
            openedBy: 99).Value;
        SetId(account, accountId);
        _educationAccounts.Accounts[accountId] = account;
        return account;
    }

    private EducationAccount AddAutomaticAccount(long accountId, long personId)
    {
        EducationAccount account = EducationAccount.OpenAutomatically(
            personId,
            $"PSEA-{personId:D8}",
            Now.AddYears(-1)).Value;
        SetId(account, accountId);
        _educationAccounts.Accounts[accountId] = account;
        return account;
    }

    private static void SetId(EducationAccount account, long id)
    {
        typeof(EducationAccount).GetProperty(nameof(EducationAccount.Id))!
            .SetValue(account, id);
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new EducationAccountTopUpModelConfiguration()]);
    }

    private sealed class FakeEducationAccountRepository : IEducationAccountRepository
    {
        public Dictionary<long, EducationAccount> Accounts { get; } = [];

        public Task<EducationAccount?> FindByIdAsync(long educationAccountId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.GetValueOrDefault(educationAccountId));

        public Task<EducationAccount?> FindByPersonIdAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.Values.SingleOrDefault(x => x.PersonId == personId));

        public Task<bool> ExistsForPersonAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult(Accounts.Values.Any(x => x.PersonId == personId));

        public Task<IReadOnlyCollection<EducationAccount>> ListActiveAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<EducationAccount>>(
                Accounts.Values.Where(x => x.StatusCode == AccountStatuses.Active).ToArray());

        public Task AddAsync(EducationAccount account, CancellationToken cancellationToken)
        {
            Accounts[account.Id] = account;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEligiblePersonLookupGateway : IEligiblePersonLookupGateway
    {
        public IReadOnlyCollection<long> AgedAtLeastPersonIds { get; set; } = [];
        public IReadOnlyCollection<long> RequestedPersonIds { get; private set; } = [];
        public int RequestedMinAge { get; private set; }
        public DateOnly RequestedToday { get; private set; }

        public Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
            DateOnly today,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<long>> FindPersonIdsAgedAtLeastAsync(
            IReadOnlyCollection<long> personIds,
            int minAge,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            RequestedPersonIds = personIds;
            RequestedMinAge = minAge;
            RequestedToday = today;
            return Task.FromResult(AgedAtLeastPersonIds);
        }
    }

    private sealed class FakePersonDirectory : IPersonDirectory
    {
        public Task<PersonSummary?> FindAsync(long personId, CancellationToken cancellationToken)
            => Task.FromResult<PersonSummary?>(new PersonSummary(
                personId,
                $"Student {personId}",
                new DateOnly(1990, 1, 1),
                "SG",
                "CITIZEN",
                10));
    }

    private sealed class FakeEmailDeliveryGateway : IEmailDeliveryGateway
    {
        public List<EmailDeliveryMessage> Messages { get; } = [];

        public Task<Result> SendAsync(
            EmailDeliveryMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public List<AuditCall> Calls { get; } = [];
        public List<SchoolAuditContext> SchoolCalls { get; } = [];

        public Task RecordAsync(
            string actionCode,
            string entityTypeCode,
            string entityId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add(new AuditCall(actionCode, entityTypeCode, entityId, detailsJson));
            return Task.CompletedTask;
        }

        public Task RecordSchoolActionAsync(
            SchoolAuditContext context,
            CancellationToken cancellationToken = default)
        {
            SchoolCalls.Add(context);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class DbUnitOfWork(MoeDbContext dbContext) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record AuditCall(
        string ActionCode,
        string EntityTypeCode,
        string EntityId,
        string? DetailsJson);
}
