using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;

namespace Moe.StudentFinance.Persistence;

public sealed class MoeDbContext(
    DbContextOptions<MoeDbContext> options,
    IEnumerable<IModelConfigurationContributor> contributors) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var contributor in contributors.OrderBy(x => x.GetType().FullName))
            contributor.Configure(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
