using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Application.RunExecution.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.TransactionResults;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TransactionResults;
using Moe.Modules.IdentityPlatform;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.RunExecution;

public sealed class TopUpTransactionResultsReaderTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly TopUpTransactionResultsReader _reader;

    public TopUpTransactionResultsReaderTests()
    {
        DbContextOptions<MoeDbContext> options =
            new DbContextOptionsBuilder<MoeDbContext>()
                .UseInMemoryDatabase($"transaction-results-{Guid.NewGuid():N}")
                .Options;

        _dbContext = new MoeDbContext(
            options,
            [
                new EducationAccountTopUpModelConfiguration(),
                new IdentityPlatformModelConfiguration()
            ]);
        _reader = new TopUpTransactionResultsReader(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();

        DateTime createdAt = new(2026, 6, 18, 1, 0, 0, DateTimeKind.Utc);
        TopUpTransaction completed = TopUpTransaction.Create(10, 101, 50m, createdAt);
        completed.Complete(9001, createdAt.AddMinutes(1));

        TopUpTransaction failed = TopUpTransaction.Create(10, 102, 75m, createdAt.AddHours(1));
        failed.Fail(SafeReasons.CreditRejected, createdAt.AddHours(1).AddMinutes(1));

        TopUpTransaction skipped = TopUpTransaction.Create(10, 103, 80m, createdAt.AddHours(2));
        skipped.Skip(SafeReasons.AccountClosed, createdAt.AddHours(2).AddMinutes(1));

        TopUpTransaction anotherRun = TopUpTransaction.Create(20, 104, 90m, createdAt.AddHours(3));
        anotherRun.Complete(9002, createdAt.AddHours(3).AddMinutes(1));

        _dbContext.AddRange(completed, failed, skipped, anotherRun);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _dbContext.DisposeAsync();

    [Fact]
    public async Task Should_Page_In_Stable_Descending_Order()
    {
        TransactionResultsPage page = await _reader.GetPageAsync(
            10,
            EmptyFilter(),
            matchingEducationAccountIds: null,
            page: 1,
            pageSize: 2,
            CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Select(x => x.EducationAccountId).Should().Equal(103, 102);
    }

    [Fact]
    public async Task Should_Apply_Status_Reason_Date_And_Account_Filters()
    {
        TopUpTransactionResultFilter filter = new(
            Status: "failed",
            StudentOrAccountSearch: "ignored-by-reader",
            Reason: "rejected",
            DateFromUtc: new DateTime(2026, 6, 18, 1, 30, 0, DateTimeKind.Utc),
            DateToUtc: new DateTime(2026, 6, 18, 2, 30, 0, DateTimeKind.Utc));

        TransactionResultsPage page = await _reader.GetPageAsync(
            10,
            filter,
            matchingEducationAccountIds: [102],
            page: 1,
            pageSize: 25,
            CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle()
            .Which.EducationAccountId.Should().Be(102);
    }

    private static TopUpTransactionResultFilter EmptyFilter()
        => new(null, null, null, null, null);
}
