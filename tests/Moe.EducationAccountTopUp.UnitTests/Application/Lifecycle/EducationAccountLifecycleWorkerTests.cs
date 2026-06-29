using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using System.Reflection;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Application.Lifecycle;

public sealed class EducationAccountLifecycleWorkerTests
{
    [Fact]
    public async Task RunIfDueAsync_WhenDisabled_DoesNotReadEligiblePeople()
    {
        FakeEligiblePersonLookupGateway people = new([1]);
        FakeAutomaticEducationAccountCreator creator = new();
        FakeAutomaticEducationAccountCloser closer = new();
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = false, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer);

        await worker.RunIfDueAsync(CancellationToken.None);

        people.Calls.Should().Be(0);
        creator.CreatedPersonIds.Should().BeEmpty();
        closer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RunIfDueAsync_WhenAtConfiguredUtcTime_ProcessesOncePerDay()
    {
        FakeEligiblePersonLookupGateway people = new([1, 2, 2]);
        FakeAutomaticEducationAccountCreator creator = new();
        FakeAutomaticEducationAccountCloser closer = new();
        FakeEducationAccountLifecycleRunRepository runs = new(enforceUniqueScheduledRuns: true);
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer,
            runs);

        await worker.RunIfDueAsync(CancellationToken.None);
        await worker.RunIfDueAsync(CancellationToken.None);

        closer.Calls.Should().Be(1);
        people.Calls.Should().Be(1);
        creator.CreatedPersonIds.Should().Equal(1, 2);
        runs.Runs.Should().ContainSingle(x =>
            x.RunDateUtc == new DateOnly(2026, 6, 24)
            && x.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled);
    }

    [Fact]
    public async Task RunIfDueAsync_WhenBeforeConfiguredUtcTime_DoesNotProcess()
    {
        FakeEligiblePersonLookupGateway people = new([1]);
        FakeAutomaticEducationAccountCreator creator = new();
        FakeAutomaticEducationAccountCloser closer = new();
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 1, 59, 0, TimeSpan.Zero)),
            people,
            creator,
            closer);

        await worker.RunIfDueAsync(CancellationToken.None);

        people.Calls.Should().Be(0);
        creator.CreatedPersonIds.Should().BeEmpty();
        closer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task RunIfDueAsync_ClosesAccountsBeforeOpeningEligibleAccounts()
    {
        List<string> events = [];
        FakeEligiblePersonLookupGateway people = new([1]);
        FakeAutomaticEducationAccountCreator creator = new(events);
        FakeAutomaticEducationAccountCloser closer = new(events);
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer);

        await worker.RunIfDueAsync(CancellationToken.None);

        events.Should().Equal("close", "open:1");
    }

    [Fact]
    public async Task ProcessAsync_ReturnsClosedAndOpenedCounts()
    {
        FakeEligiblePersonLookupGateway people = new([1, 2, 2, 3]);
        FakeAutomaticEducationAccountCreator creator = new(createdPersonIds: [1, 3]);
        FakeAutomaticEducationAccountCloser closer = new(activeAccountCount: 5, closedCount: 2);
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer);

        EducationAccountLifecycleRunResult result = await worker.ProcessAsync(
            new DateOnly(2026, 6, 24),
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        result.ClosedCount.Should().Be(2);
        result.OpenedCount.Should().Be(2);
        creator.CreatedPersonIds.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ProcessAsync_Records_Completed_Run_With_Created_And_Closed_Items()
    {
        FakeEligiblePersonLookupGateway people = new([10, 11]);
        FakeAutomaticEducationAccountCreator creator = new(createdPersonIds: [10]);
        FakeAutomaticEducationAccountCloser closer = new(
            activeAccountCount: 2,
            closedResults:
            [
                new AutomaticEducationAccountClosureResult(
                    EducationAccountId: 7001,
                    PersonId: 30,
                    Closed: true),
                new AutomaticEducationAccountClosureResult(
                    EducationAccountId: 7002,
                    PersonId: 31,
                    Closed: false)
            ]);
        FakeEducationAccountLifecycleRunRepository runs = new();
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer,
            runs);

        EducationAccountLifecycleRunResult result = await worker.ProcessAsync(
            new DateOnly(2026, 6, 24),
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero),
            EducationAccountLifecycleRunTriggerTypes.Manual,
            CancellationToken.None);

        result.OpenedCount.Should().Be(1);
        result.ClosedCount.Should().Be(1);
        EducationAccountLifecycleRun run = runs.Runs.Should().ContainSingle().Subject;
        run.TriggerTypeCode.Should().Be(EducationAccountLifecycleRunTriggerTypes.Manual);
        run.StatusCode.Should().Be(EducationAccountLifecycleRunStatusCodes.Completed);
        run.OpenedCount.Should().Be(1);
        run.ClosedCount.Should().Be(1);
        run.Items.Should().HaveCount(2);
        run.Items.Should().Contain(x =>
            x.ActionCode == EducationAccountLifecycleRunItemActionCodes.Closed
            && x.PersonId == 30
            && x.EducationAccountId == 7001);
        run.Items.Should().Contain(x =>
            x.ActionCode == EducationAccountLifecycleRunItemActionCodes.Created
            && x.PersonId == 10
            && x.EducationAccountId == 10);
    }

    [Fact]
    public async Task ProcessAsync_WhenScheduledRunAlreadyClaimed_Skips_WithoutProcessingAgain()
    {
        DateOnly today = new(2026, 6, 24);
        FakeEligiblePersonLookupGateway people = new([10]);
        FakeAutomaticEducationAccountCreator creator = new();
        FakeAutomaticEducationAccountCloser closer = new();
        FakeEducationAccountLifecycleRunRepository runs = new(enforceUniqueScheduledRuns: true);
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer,
            runs);

        EducationAccountLifecycleRunResult first = await worker.ProcessAsync(
            today,
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero),
            EducationAccountLifecycleRunTriggerTypes.Scheduled,
            CancellationToken.None);
        EducationAccountLifecycleRunResult second = await worker.ProcessAsync(
            today,
            new DateTimeOffset(2026, 6, 24, 2, 1, 0, TimeSpan.Zero),
            EducationAccountLifecycleRunTriggerTypes.Scheduled,
            CancellationToken.None);

        first.Should().BeEquivalentTo(new { OpenedCount = 1, ClosedCount = 0, Skipped = false });
        second.Should().BeEquivalentTo(new { OpenedCount = 0, ClosedCount = 0, Skipped = true });
        closer.Calls.Should().Be(1);
        people.Calls.Should().Be(1);
        creator.CreatedPersonIds.Should().Equal(10);
        runs.Runs.Should().ContainSingle(x =>
            x.RunDateUtc == today
            && x.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled);
    }

    [Fact]
    public async Task ProcessAsync_WhenManualRunsRepeatSameDate_AllRowsAreWritten()
    {
        DateOnly today = new(2026, 6, 24);
        FakeEligiblePersonLookupGateway people = new(Array.Empty<long>());
        FakeAutomaticEducationAccountCreator creator = new();
        FakeAutomaticEducationAccountCloser closer = new();
        FakeEducationAccountLifecycleRunRepository runs = new(enforceUniqueScheduledRuns: true);
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer,
            runs);

        await worker.ProcessAsync(
            today,
            new DateTimeOffset(2026, 6, 24, 7, 0, 0, TimeSpan.Zero),
            EducationAccountLifecycleRunTriggerTypes.Manual,
            CancellationToken.None);
        await worker.ProcessAsync(
            today,
            new DateTimeOffset(2026, 6, 24, 7, 1, 0, TimeSpan.Zero),
            EducationAccountLifecycleRunTriggerTypes.Manual,
            CancellationToken.None);

        runs.Runs
            .Where(x =>
                x.RunDateUtc == today
                && x.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Manual)
            .Should()
            .HaveCount(2);
    }

    private static EducationAccountLifecycleWorker CreateWorker(
        EducationAccountLifecycleOptions options,
        IClock clock,
        FakeEligiblePersonLookupGateway people,
        FakeAutomaticEducationAccountCreator creator,
        FakeAutomaticEducationAccountCloser closer,
        FakeEducationAccountLifecycleRunRepository? runs = null)
    {
        runs ??= new FakeEducationAccountLifecycleRunRepository();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEligiblePersonLookupGateway>(people)
            .AddSingleton<IAutomaticEducationAccountCreator>(creator)
            .AddSingleton<IAutomaticEducationAccountCloser>(closer)
            .AddSingleton<IEducationAccountLifecycleRunRepository>(runs)
            .AddSingleton<IUnitOfWork>(new FakeUnitOfWork(runs))
            .BuildServiceProvider();

        return new EducationAccountLifecycleWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            NullLogger<EducationAccountLifecycleWorker>.Instance,
            clock);
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class FakeEligiblePersonLookupGateway(IReadOnlyCollection<long> personIds)
        : IEligiblePersonLookupGateway
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyCollection<long>> FindEligibleForEducationAccountAsync(
            DateOnly today,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(personIds);
        }

        public Task<IReadOnlyCollection<long>> FindPersonIdsAgedAtLeastAsync(
            IReadOnlyCollection<long> personIds,
            int minAge,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeAutomaticEducationAccountCreator(
        List<string>? events = null,
        IReadOnlyCollection<long>? createdPersonIds = null) : IAutomaticEducationAccountCreator
    {
        private readonly IReadOnlyCollection<long>? _createdPersonIds = createdPersonIds;
        public List<long> CreatedPersonIds { get; } = [];

        public Task<AutomaticEducationAccountCreationResult> EnsureCreatedAsync(
            long personId,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken)
        {
            CreatedPersonIds.Add(personId);
            events?.Add($"open:{personId}");
            bool created = _createdPersonIds is null || _createdPersonIds.Contains(personId);
            return Task.FromResult(new AutomaticEducationAccountCreationResult(
                personId,
                $"PSEA-{personId:D8}",
                Created: created));
        }
    }

    private sealed class FakeAutomaticEducationAccountCloser(
        List<string>? events = null,
        int activeAccountCount = 0,
        int closedCount = 0,
        IReadOnlyCollection<AutomaticEducationAccountClosureResult>? closedResults = null) : IAutomaticEducationAccountCloser
    {
        public int Calls { get; private set; }

        public Task<AutomaticEducationAccountClosureSummary> CloseEligibleAsync(
            DateOnly today,
            DateTimeOffset closedAtUtc,
            CancellationToken cancellationToken)
        {
            Calls++;
            events?.Add("close");
            if (closedResults is not null)
            {
                return Task.FromResult(new AutomaticEducationAccountClosureSummary(
                    ActiveAccountCount: activeAccountCount,
                    ClosedCount: closedResults.Count(x => x.Closed),
                    Results: closedResults));
            }

            return Task.FromResult(new AutomaticEducationAccountClosureSummary(
                ActiveAccountCount: activeAccountCount,
                ClosedCount: closedCount,
                Results: []));
        }

        public Task<AutomaticEducationAccountClosureResult> EnsureClosedAsync(
            Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount account,
            DateTimeOffset closedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeEducationAccountLifecycleRunRepository(bool enforceUniqueScheduledRuns = false)
        : IEducationAccountLifecycleRunRepository
    {
        private readonly bool _enforceUniqueScheduledRuns = enforceUniqueScheduledRuns;
        private readonly List<EducationAccountLifecycleRun> _pendingRuns = [];
        public List<EducationAccountLifecycleRun> Runs { get; } = [];

        public Task AddAsync(
            EducationAccountLifecycleRun run,
            CancellationToken cancellationToken)
        {
            _pendingRuns.Add(run);
            return Task.CompletedTask;
        }

        public void SaveChanges()
        {
            if (_enforceUniqueScheduledRuns)
            {
                EducationAccountLifecycleRun? duplicateScheduledRun = _pendingRuns.FirstOrDefault(pending =>
                    pending.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled
                    && Runs.Any(committed =>
                        committed.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Scheduled
                        && committed.RunDateUtc == pending.RunDateUtc));

                if (duplicateScheduledRun is not null)
                {
                    _pendingRuns.Remove(duplicateScheduledRun);
                    throw new DbUpdateException(
                        "Cannot insert duplicate key row.",
                        CreateSqlException(2601));
                }
            }

            Runs.AddRange(_pendingRuns);
            _pendingRuns.Clear();
        }

        private static SqlException CreateSqlException(int number)
        {
            ConstructorInfo errorConstructor = typeof(SqlError)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(constructor => constructor.GetParameters().Length == 8);
            object?[] args =
            [
                number,
                (byte)0,
                (byte)0,
                "server",
                "duplicate key",
                "procedure",
                1,
                null
            ];
            var error = (SqlError)errorConstructor.Invoke(args);
            var errors = (SqlErrorCollection)Activator.CreateInstance(
                typeof(SqlErrorCollection),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
            typeof(SqlErrorCollection)
                .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(errors, [error]);
            return (SqlException)typeof(SqlException)
                .GetMethod(
                    "CreateException",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(SqlErrorCollection), typeof(string)],
                    modifiers: null)!
                .Invoke(null, [errors, "15.0"])!;
        }
    }

    private sealed class FakeUnitOfWork(FakeEducationAccountLifecycleRunRepository runs) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            runs.SaveChanges();
            return Task.FromResult(1);
        }
    }
}
