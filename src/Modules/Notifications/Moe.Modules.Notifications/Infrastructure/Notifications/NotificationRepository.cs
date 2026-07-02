using Microsoft.EntityFrameworkCore;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.Notifications.Infrastructure.Notifications;

public sealed class NotificationRepository(MoeDbContext dbContext) : INotificationRepository
{
    public Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => dbContext.Set<Notification>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetMyNotificationsAsync(long userAccountId, int take, CancellationToken cancellationToken)
        => await dbContext.Set<Notification>()
            .AsNoTracking()
            .Where(x => x.RecipientUserAccountId == userAccountId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<long> GetUnreadCountAsync(long userAccountId, CancellationToken cancellationToken)
        => dbContext.Set<Notification>().LongCountAsync(x => x.RecipientUserAccountId == userAccountId && x.ReadAtUtc == null, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
