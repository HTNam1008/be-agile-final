using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.FasPayment;
using Moe.Modules.IdentityPlatform;
using Moe.StudentFinance.Migrations;
using Moe.StudentFinance.Persistence;

namespace Moe.FasPayment.SqlTests;

internal sealed class SqlFasTestDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _connectionString;

    private SqlFasTestDatabase(string databaseName, string connectionString)
    {
        _databaseName = databaseName;
        _connectionString = connectionString;
    }

    public static bool IsEnabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MOE_FAS_TEST_CONNECTION"));

    public static async Task<SqlFasTestDatabase> CreateAsync(string? targetMigration = null)
    {
        string template = Environment.GetEnvironmentVariable("MOE_FAS_TEST_CONNECTION")
            ?? throw new InvalidOperationException("MOE_FAS_TEST_CONNECTION is required for SQL tests.");
        string databaseName = $"MOEFasAoe_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder(template) { InitialCatalog = databaseName };
        var database = new SqlFasTestDatabase(databaseName, builder.ConnectionString);
        await using MoeDbContext context = database.CreateContext();
        if (targetMigration is null)
            await context.Database.MigrateAsync();
        else
            await context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(targetMigration);
        return database;
    }

    public MoeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(DesignTimeMoeDbContextFactory).Assembly.FullName))
            .Options;
        IModelConfigurationContributor[] contributors =
        [
            new IdentityPlatformModelConfiguration(),
            new EducationAccountTopUpModelConfiguration(),
            new CourseBillingModelConfiguration(),
            new FasPaymentModelConfiguration()
        ];
        return new MoeDbContext(options, contributors);
    }

    public async ValueTask DisposeAsync()
    {
        var masterBuilder = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
    }
}
