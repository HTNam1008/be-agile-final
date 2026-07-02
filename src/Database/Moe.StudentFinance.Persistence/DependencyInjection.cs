using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Persistence;

namespace Moe.StudentFinance.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddMoePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MoeDatabase")
            ?? throw new InvalidOperationException("Connection string 'MoeDatabase' not found.");

        DatabaseOptions databaseOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<MoeDbContext>((sp, options) =>
        {
            if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Mode=Memory", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                {
                    if (databaseOptions.EnableSqlRetry)
                    {
                        sql.EnableRetryOnFailure(
                            databaseOptions.MaxRetryCount,
                            TimeSpan.FromSeconds(databaseOptions.MaxRetryDelaySeconds),
                            errorNumbersToAdd: null);
                    }

                    if (databaseOptions.CommandTimeoutSeconds > 0)
                    {
                        sql.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    }

                    sql.MigrationsAssembly("Moe.StudentFinance.Migrations");
                });
            }
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MoeDbContext>());
        services.AddScoped<IDistributedLock, SqlDistributedLock>();
        services.AddScoped<ITransactionalExecutor>(sp => sp.GetRequiredService<MoeDbContext>());
        return services;
    }
}

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool EnableSqlRetry { get; set; } = true;

    public int MaxRetryCount { get; set; } = 3;

    public int MaxRetryDelaySeconds { get; set; } = 10;
}
