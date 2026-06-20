using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution;
using Moe.Modules.EducationAccountTopUp.Application.TopUps.ExecuteRun;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class ExecuteTopUpRunCommandHandlerTests
{
    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EducationAccount>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaign>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaignRule>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpRun>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpTransaction>().HasKey(x => x.Id);
            modelBuilder.Entity<AccountTransaction>().HasKey(x => x.Id);
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<SchoolEnrollment>().HasKey(x => x.Id);
            modelBuilder.Entity<TopUpCampaignRecipient>().HasKey(x => x.Id);
        }
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new MoeDbContext(options, new[] { new TestModelConfigurationContributor() });
    }

    private sealed class MockCurrentUser : ICurrentUser
    {
        public long? UserAccountId => 1;
        public long? OrganizationUnitId => 10;
        public System.Collections.Generic.IReadOnlyCollection<long> OrganizationUnitIds => new[] { 10L };
        public string SessionId => "test";
        public long? PersonId => null;
        public System.Collections.Generic.IReadOnlyCollection<string> Roles => Array.Empty<string>();
        public System.Collections.Generic.IReadOnlyCollection<string> Permissions => Array.Empty<string>();
        public string Portal => "Admin";
        public bool IsAuthenticated => true;
        public bool HasPermission(string permission) => true;
    }

    private sealed class MockClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class AllowAllAdminAccess : IAdminAccessControl
    {
        public bool IsHqAdmin => true;
        public bool IsSchoolAdmin => false;
        public System.Collections.Generic.IReadOnlyCollection<long> ScopedOrganizationIds => Array.Empty<long>();
        public bool CanAccessOrganization(long organizationId) => true;
        public Moe.SharedKernel.Results.Result EnsureCanAccessOrganization(long organizationId) => Moe.SharedKernel.Results.Result.Success();
        public AdminOrganizationScope ResolveOrganizationFilter(long? requestedOrganizationId)
            => new(true, true, requestedOrganizationId, Array.Empty<long>());
    }

    private sealed class MockEventPublisher : ITopUpExecutionEventPublisher
    {
        public Task PublishTopUpFailedEvent(long transactionId, string reason) => Task.CompletedTask;
        public Task PublishTopUpSucceededEvent(long transactionId, decimal amount) => Task.CompletedTask;
        public Task PublishRunStartedAsync(TopUpRunStartedReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishRunCompletedAsync(TopUpRunCompletedReport report, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishTopUpReceivedAsync(TopUpReceivedReport report, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MockMetrics : ITopUpExecutionMetrics
    {
        public void RecordTopUpSuccess(decimal amount) {}
        public void RecordTopUpFailure() {}
        public void RecordProcessingTime(TimeSpan duration) {}
        public void RecordRunStarted() {}
        public void RecordRunCompleted(int succeeded, int failed) {}
        public void RecordRecipientProcessed(long recipientId, string campaignCode, bool isDynamic, bool wasEligible) {}
        public void RecordAccountCreditDbConflict() {}
        public void RecordRunStarted(long runId, long campaignId, int totalRecipients) {}
        public void RecordRunCompleted(long runId, long campaignId, string status, int totalRecipients, int succeeded, int failed, int skipped, TimeSpan duration) {}
    }

    [Fact]
    public async Task Handle_ShouldChunkAndExecute_DynamicRules()
    {
        // Arrange
        using var dbContext = CreateDbContext();

        var nowUtc = DateTime.UtcNow;

        var campaign = TopUpCampaign.Create(10, "TEST-DYN", "Test Dynamic", null, "DYNAMIC_RULES", 150m, "Reason", "IMMEDIATE", new DateOnly(2026, 1, 1), null, null, null, 1, nowUtc);
        campaign.ChangeStatus("ACTIVE", 1, nowUtc);
        dbContext.Set<TopUpCampaign>().Add(campaign);

        // Add a rule that matches balance > 50
        var rule = TopUpCampaignRule.Create(campaign.Id, "ACCOUNTBALANCE", "GREATERTHAN", 50m, null, null);
        dbContext.Set<TopUpCampaignRule>().Add(rule);

        // Add 3 accounts: 2 match, 1 fails
        var acc1 = EducationAccount.OpenManual(1, "EA-001", nowUtc, "Reason", "Remarks", 1).Value;
        acc1.UpdateBalance(100m); // Matches
        dbContext.Set<EducationAccount>().Add(acc1);

        var acc2 = EducationAccount.OpenManual(2, "EA-002", nowUtc, "Reason", "Remarks", 1).Value;
        acc2.UpdateBalance(200m); // Matches
        dbContext.Set<EducationAccount>().Add(acc2);

        var acc3 = EducationAccount.OpenManual(3, "EA-003", nowUtc, "Reason", "Remarks", 1).Value;
        acc3.UpdateBalance(0m); // Fails
        dbContext.Set<EducationAccount>().Add(acc3);

        await dbContext.SaveChangesAsync();

        var processor = new RecipientProcessingService(
            new TopUpTransactionRepository(dbContext),
            new StubAccountCreditGateway(dbContext, NullLogger<StubAccountCreditGateway>.Instance),
            new StubRecipientValidator(dbContext),
            new MockEventPublisher(),
            new MockMetrics(),
            dbContext,
            new MockClock(),
            NullLogger<RecipientProcessingService>.Instance);
        var handler = new ExecuteTopUpRunCommandHandler(dbContext, new MockCurrentUser(), new AllowAllAdminAccess(), new MockClock(), processor);
        var command = new ExecuteTopUpRunCommand(campaign.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(because: result.IsFailure ? result.Error.Message : "No Error");
        
        var runId = result.Value;
        var run = await dbContext.Set<TopUpRun>().FindAsync(runId);
        run.Should().NotBeNull();
        run!.TotalProcessed.Should().Be(2); // Only 2 accounts matched
        run.TotalSucceeded.Should().Be(2);
        run.TotalAmount.Should().Be(300m); // 2 * 150
        
        var transactions = await dbContext.Set<TopUpTransaction>().Where(t => t.TopUpRunId == runId).ToListAsync();
        transactions.Should().HaveCount(2);
        transactions.Should().Contain(t => t.EducationAccountId == acc1.Id);
        transactions.Should().Contain(t => t.EducationAccountId == acc2.Id);
    }
}
