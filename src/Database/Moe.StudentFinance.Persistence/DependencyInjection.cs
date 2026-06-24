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
                    sql.EnableRetryOnFailure(3);
                    sql.MigrationsAssembly("Moe.StudentFinance.Migrations");
                });
            }
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MoeDbContext>());
        services.AddScoped<ITransactionalExecutor>(sp => sp.GetRequiredService<MoeDbContext>());
        return services;
    }
}
