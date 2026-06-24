using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.EducationAccountTopUp.Application.Lifecycle.RunHistory;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.EducationAccountTopUp.Infrastructure.History;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.History;

public sealed class EducationAccountLifecycleRunHistoryTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly EducationAccountLifecycleHistoryReader _reader;

    public EducationAccountLifecycleRunHistoryTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"education-account-lifecycle-history-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(
            options,
            [new LifecycleHistoryTestModelConfiguration()]);
        _reader = new EducationAccountLifecycleHistoryReader(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        EducationAccount accountOne = EducationAccount.OpenAutomatically(
            9001,
            "PSEA-00009001",
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)).Value;
        EducationAccount accountTwo = EducationAccount.OpenAutomatically(
            9002,
            "PSEA-00009002",
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero)).Value;
        _dbContext.AddRange(accountOne, accountTwo);
        await _dbContext.SaveChangesAsync();

        EducationAccountLifecycleRun manualRun = CreateCompletedRun(
            EducationAccountLifecycleRunTriggerTypes.Manual,
            new DateTimeOffset(2026, 6, 24, 7, 0, 0, TimeSpan.Zero),
            openedCount: 1,
            closedCount: 1);
        manualRun.AddItem(
            9001,
            accountOne.Id,
            EducationAccountLifecycleRunItemActionCodes.Created,
            new DateTimeOffset(2026, 6, 24, 7, 0, 1, TimeSpan.Zero));
        manualRun.AddItem(
            9002,
            accountTwo.Id,
            EducationAccountLifecycleRunItemActionCodes.Closed,
            new DateTimeOffset(2026, 6, 24, 7, 0, 2, TimeSpan.Zero));

        EducationAccountLifecycleRun scheduledRun = CreateCompletedRun(
            EducationAccountLifecycleRunTriggerTypes.Scheduled,
            new DateTimeOffset(2026, 6, 24, 2, 0, 0, TimeSpan.Zero),
            openedCount: 0,
            closedCount: 0);

        EducationAccountLifecycleRun olderRun = CreateCompletedRun(
            EducationAccountLifecycleRunTriggerTypes.Scheduled,
            new DateTimeOffset(2026, 6, 23, 2, 0, 0, TimeSpan.Zero),
            openedCount: 1,
            closedCount: 0);

        _dbContext.AddRange(manualRun, scheduledRun, olderRun);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task List_Should_Filter_By_Run_Date_And_Return_Newest_First()
    {
        GetEducationAccountLifecycleRunsHandler handler = new(_reader);

        var result = await handler.Handle(
            new GetEducationAccountLifecycleRunsQuery(
                FromDate: new DateOnly(2026, 6, 24),
                ToDate: new DateOnly(2026, 6, 24),
                Page: 1,
                PageSize: 25),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        PageResponse<EducationAccountLifecycleRunListItem> page = result.Value;
        page.TotalCount.Should().Be(2);
        page.Items.Select(x => x.TriggerTypeCode).Should().Equal(
            EducationAccountLifecycleRunTriggerTypes.Manual,
            EducationAccountLifecycleRunTriggerTypes.Scheduled);
        page.Items[0].OpenedCount.Should().Be(1);
        page.Items[0].ClosedCount.Should().Be(1);
    }

    [Fact]
    public async Task Detail_Should_Return_Header_Items_And_Person_Display()
    {
        EducationAccountLifecycleRun run = await _dbContext
            .Set<EducationAccountLifecycleRun>()
            .Include(x => x.Items)
            .SingleAsync(x => x.TriggerTypeCode == EducationAccountLifecycleRunTriggerTypes.Manual);
        FakeLifecyclePersonDisplayGateway people = new(
            [
                new LifecyclePersonDisplay(9001, "Created Student", "S****001A", "North View Secondary School"),
                new LifecyclePersonDisplay(9002, "Closed Student", "S****002B", "West Coast Junior College")
            ]);
        GetEducationAccountLifecycleRunDetailHandler handler = new(_reader, people);

        var result = await handler.Handle(
            new GetEducationAccountLifecycleRunDetailQuery(run.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        EducationAccountLifecycleRunDetail detail = result.Value;
        detail.RunId.Should().Be(run.Id);
        detail.Items.Should().HaveCount(2);
        detail.Items.Should().Contain(x =>
            x.PersonId == 9001
            && x.FullName == "Created Student"
            && x.MaskedNric == "S****001A"
            && x.SchoolName == "North View Secondary School"
            && x.AccountNumber == "PSEA-00009001"
            && x.ActionCode == EducationAccountLifecycleRunItemActionCodes.Created);
        detail.Items.Should().Contain(x =>
            x.PersonId == 9002
            && x.FullName == "Closed Student"
            && x.MaskedNric == "S****002B"
            && x.SchoolName == "West Coast Junior College"
            && x.AccountNumber == "PSEA-00009002"
            && x.ActionCode == EducationAccountLifecycleRunItemActionCodes.Closed);
    }

    private static EducationAccountLifecycleRun CreateCompletedRun(
        string triggerTypeCode,
        DateTimeOffset startedAtUtc,
        int openedCount,
        int closedCount)
    {
        EducationAccountLifecycleRun run = EducationAccountLifecycleRun.Start(
            DateOnly.FromDateTime(startedAtUtc.UtcDateTime),
            startedAtUtc,
            triggerTypeCode);
        run.Complete(openedCount, closedCount, startedAtUtc.AddSeconds(10));
        return run;
    }

    private sealed class FakeLifecyclePersonDisplayGateway(
        IReadOnlyCollection<LifecyclePersonDisplay> people)
        : ILifecyclePersonDisplayGateway
    {
        public Task<IReadOnlyCollection<LifecyclePersonDisplay>> FindByPersonIdsAsync(
            IReadOnlyCollection<long> personIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<LifecyclePersonDisplay> matches = people
                .Where(x => personIds.Contains(x.PersonId))
                .ToArray();
            return Task.FromResult(matches);
        }
    }

    private sealed class LifecycleHistoryTestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EducationAccount>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
                builder.Ignore(x => x.DomainEvents);
            });

            modelBuilder.ApplyConfiguration(new EducationAccountLifecycleRunConfiguration());
            modelBuilder.ApplyConfiguration(new EducationAccountLifecycleRunItemConfiguration());
        }
    }
}
