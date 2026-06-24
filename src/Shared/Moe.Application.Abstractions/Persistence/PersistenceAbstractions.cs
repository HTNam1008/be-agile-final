using Microsoft.EntityFrameworkCore;

namespace Moe.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITransactionalExecutor
{
    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}

public interface IModelConfigurationContributor
{
    void Configure(ModelBuilder modelBuilder);
}
