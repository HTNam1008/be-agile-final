using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moe.Application.Abstractions.Persistence;

namespace Moe.StudentFinance.Persistence;

public sealed class MoeDbContext(
    DbContextOptions<MoeDbContext> options,
    IEnumerable<IModelConfigurationContributor> contributors) : DbContext(options), IUnitOfWork, ITransactionalExecutor
{
    internal string ModelConfigurationCacheKey { get; } = string.Join(
        "|",
        contributors
            .Select(x => x.GetType().FullName)
            .OrderBy(x => x, StringComparer.Ordinal));

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var coreExtension = optionsBuilder.Options.Extensions
            .OfType<CoreOptionsExtension>()
            .FirstOrDefault();

        if (coreExtension?.InternalServiceProvider is null)
        {
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, MoeDbContextModelCacheKeyFactory>();
        }

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var contributor in contributors.OrderBy(x => x.GetType().FullName))
            contributor.Configure(modelBuilder);

        if (Database.IsSqlite())
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties()
                    .Where(p => p.IsConcurrencyToken && p.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate && p.ClrType == typeof(byte[]));
                foreach (var property in properties)
                {
                    property.SetDefaultValue(Array.Empty<byte>());
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            return operation(cancellationToken);
        }

        var strategy = Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            T result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }
}

internal sealed class MoeDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        return context is MoeDbContext moeContext
            ? (context.GetType(), moeContext.ModelConfigurationCacheKey, designTime)
            : (context.GetType(), designTime);
    }
}
