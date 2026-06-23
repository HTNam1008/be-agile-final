using Microsoft.EntityFrameworkCore;
using Moe.Modules.FasPayment.IGateway.Payments;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Infrastructure.Repositories;

internal sealed class PaymentPersistenceTracker(MoeDbContext dbContext) : IPaymentPersistenceTracker
{
    public bool IsRetryablePersistenceConflict(Exception exception)
        => exception is DbUpdateConcurrencyException or DbUpdateException;

    public string DescribeConflict(Exception exception)
    {
        if (exception is not DbUpdateConcurrencyException concurrencyException)
        {
            return exception.GetType().Name;
        }

        return string.Join(", ", concurrencyException.Entries.Select(entry =>
        {
            string keys = string.Join(",", entry.Properties
                .Where(property => property.Metadata.IsPrimaryKey())
                .Select(property => $"{property.Metadata.Name}={property.CurrentValue}"));
            return $"{entry.Metadata.ClrType.Name}({keys})";
        }));
    }

    public void ClearTrackedChanges()
        => dbContext.ChangeTracker.Clear();
}
