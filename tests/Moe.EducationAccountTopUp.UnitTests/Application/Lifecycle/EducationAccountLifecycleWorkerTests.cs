using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.Accounts;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
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
        EducationAccountLifecycleWorker worker = CreateWorker(
            new EducationAccountLifecycleOptions { Enabled = true, RunAtUtc = "02:00" },
            new FakeClock(new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)),
            people,
            creator,
            closer);

        await worker.RunIfDueAsync(CancellationToken.None);
        await worker.RunIfDueAsync(CancellationToken.None);

        closer.Calls.Should().Be(1);
        people.Calls.Should().Be(1);
        creator.CreatedPersonIds.Should().Equal(1, 2);
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

    private static EducationAccountLifecycleWorker CreateWorker(
        EducationAccountLifecycleOptions options,
        IClock clock,
        FakeEligiblePersonLookupGateway people,
        FakeAutomaticEducationAccountCreator creator,
        FakeAutomaticEducationAccountCloser closer)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IEligiblePersonLookupGateway>(people)
            .AddSingleton<IAutomaticEducationAccountCreator>(creator)
            .AddSingleton<IAutomaticEducationAccountCloser>(closer)
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

    private sealed class FakeAutomaticEducationAccountCreator(List<string>? events = null) : IAutomaticEducationAccountCreator
    {
        public List<long> CreatedPersonIds { get; } = [];

        public Task<AutomaticEducationAccountCreationResult> EnsureCreatedAsync(
            long personId,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken)
        {
            CreatedPersonIds.Add(personId);
            events?.Add($"open:{personId}");
            return Task.FromResult(new AutomaticEducationAccountCreationResult(
                personId,
                $"PSEA-{personId:D8}",
                Created: true));
        }
    }

    private sealed class FakeAutomaticEducationAccountCloser(List<string>? events = null) : IAutomaticEducationAccountCloser
    {
        public int Calls { get; private set; }

        public Task<AutomaticEducationAccountClosureSummary> CloseEligibleAsync(
            DateOnly today,
            DateTimeOffset closedAtUtc,
            CancellationToken cancellationToken)
        {
            Calls++;
            events?.Add("close");
            return Task.FromResult(new AutomaticEducationAccountClosureSummary(
                ActiveAccountCount: 0,
                ClosedCount: 0));
        }

        public Task<AutomaticEducationAccountClosureResult> EnsureClosedAsync(
            Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts.EducationAccount account,
            DateTimeOffset closedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
