using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Infrastructure.History;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.History;

public sealed class AccountTransactionHistoryReaderTests : IAsyncLifetime
{
    private readonly MoeDbContext _dbContext;
    private readonly AccountTransactionHistoryReader _reader;

    public AccountTransactionHistoryReaderTests()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"account-transaction-history-{Guid.NewGuid():N}")
            .Options;

        _dbContext = new MoeDbContext(
            options,
            [new AccountTransactionHistoryTestModelConfiguration()]);

        _reader = new AccountTransactionHistoryReader(_dbContext);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetTransactionsAsync_ReturnsNewestFirstAndStablePage()
    {
        _dbContext.AddRange(
            CreateTransaction(educationAccountId: 1001, amount: 10m, balanceAfter: 10m, atUtc: new DateTime(2026, 6, 20, 8, 0, 0, DateTimeKind.Utc), idempotencyKey: "txn-old"),
            CreateTransaction(educationAccountId: 1001, amount: 20m, balanceAfter: 30m, atUtc: new DateTime(2026, 6, 22, 8, 0, 0, DateTimeKind.Utc), idempotencyKey: "txn-new"),
            CreateTransaction(educationAccountId: 1001, amount: 15m, balanceAfter: 45m, atUtc: new DateTime(2026, 6, 21, 8, 0, 0, DateTimeKind.Utc), idempotencyKey: "txn-mid"),
            CreateTransaction(educationAccountId: 2002, amount: 99m, balanceAfter: 99m, atUtc: new DateTime(2026, 6, 23, 8, 0, 0, DateTimeKind.Utc), idempotencyKey: "txn-other-account"));
        await _dbContext.SaveChangesAsync();

        var page = await _reader.GetTransactionsAsync(
            educationAccountId: 1001,
            page: 2,
            pageSize: 1,
            CancellationToken.None);

        page.TotalCount.Should().Be(3);
        page.Items.Should().ContainSingle();
        page.Items[0].Amount.Should().Be(15m);
        page.Items[0].BalanceAfter.Should().Be(45m);
        page.Items[0].Description.Should().Be("Top-up txn-mid");
        page.Items[0].ReferenceTypeCode.Should().Be("TOPUP");
        page.Items[0].TransactionTypeCode.Should().Be("CREDIT");
    }

    [Fact]
    public async Task GetTransactionsAsync_OnNoTransactions_ReturnsEmptyPage()
    {
        var page = await _reader.GetTransactionsAsync(
            educationAccountId: 9999,
            page: 1,
            pageSize: 20,
            CancellationToken.None);

        page.TotalCount.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    private static AccountTransaction CreateTransaction(
        long educationAccountId,
        decimal amount,
        decimal balanceAfter,
        DateTime atUtc,
        string idempotencyKey)
        => AccountTransaction.Create(
            educationAccountId,
            "CREDIT",
            amount,
            "TOPUP",
            referenceId: null,
            idempotencyKey,
            currentBalance: balanceAfter - amount,
            description: $"Top-up {idempotencyKey}",
            createdByUserId: null,
            atUtc);

    private sealed class AccountTransactionHistoryTestModelConfiguration : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AccountTransaction>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Id).ValueGeneratedOnAdd();
            });
        }
    }
}
