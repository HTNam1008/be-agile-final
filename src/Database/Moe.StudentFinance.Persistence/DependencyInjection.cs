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
            ?? throw new InvalidOperationException("Connection string 'MoeDatabase' is required.");
        services.AddDbContext<MoeDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(3);
            sql.MigrationsAssembly("Moe.StudentFinance.Migrations");
        }));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MoeDbContext>());
        services.AddScoped<ITransactionalExecutor>(sp => sp.GetRequiredService<MoeDbContext>());
        return services;
    }
}
