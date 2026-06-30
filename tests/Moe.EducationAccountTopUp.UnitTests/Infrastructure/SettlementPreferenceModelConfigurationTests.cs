using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests.Infrastructure;

public sealed class SettlementPreferenceModelConfigurationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 30, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SettlementPreference_EnforcesSingleActivePreferencePerEducationAccount()
    {
        using MoeDbContext dbContext = CreateDbContext();

        IEntityType entityType = dbContext.Model.FindEntityType(typeof(SettlementPreference))!;

        IIndex activeIndex = entityType.GetIndexes()
            .Single(index => index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SettlementPreference.EducationAccountId)]));

        activeIndex.IsUnique.Should().BeTrue();
        activeIndex.GetFilter().Should().Be("[IsActive] = 1");
    }

    [Fact]
    public void SettlementPreference_HasForeignKeyToEducationAccount()
    {
        using MoeDbContext dbContext = CreateDbContext();

        IEntityType entityType = dbContext.Model.FindEntityType(typeof(SettlementPreference))!;

        IForeignKey foreignKey = entityType.GetForeignKeys()
            .Single(fk => fk.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(SettlementPreference.EducationAccountId)]));

        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(EducationAccount));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public async Task SettlementPreference_DatabaseRejectsTwoActiveRowsForSameEducationAccount()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using MoeDbContext dbContext = CreateSqliteDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        EducationAccount account = EducationAccount.OpenManual(
            9101,
            "EA-SETTLE-9101",
            Now,
            "TEST",
            "Unique active settlement preference test",
            openedBy: 42).Value;
        dbContext.Set<EducationAccount>().Add(account);
        await dbContext.SaveChangesAsync();

        dbContext.Set<SettlementPreference>().AddRange(
            SettlementPreference.Create(
                account.Id,
                SettlementDestinationTypeCodes.Cpf,
                "CPF_DEFAULT",
                "CPF account (linked to NRIC)",
                isVerified: true,
                Now.UtcDateTime),
            SettlementPreference.Create(
                account.Id,
                SettlementDestinationTypeCodes.Bank,
                "{\"bankName\":\"DBS\",\"accountNumber\":\"123456789\"}",
                "DBS account ending 6789",
                isVerified: false,
                Now.UtcDateTime));

        Func<Task> act = () => dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MoeDbContext(options, [new EducationAccountTopUpModelConfiguration()]);
    }

    private static MoeDbContext CreateSqliteDbContext(SqliteConnection connection)
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlite(connection)
            .Options;

        return new MoeDbContext(options, [new EducationAccountTopUpModelConfiguration()]);
    }
}
