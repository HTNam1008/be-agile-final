using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.MailDelivery.Domain;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.MailDelivery.Application.Admin;

internal sealed class MailNotificationAdminService(
    MoeDbContext dbContext,
    IClock clock) : IMailNotificationAdminService
{
    public async Task<PageResponse<MailNotificationListItem>> ListAsync(
        MailNotificationFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        int safePage = Math.Max(1, page);
        int safePageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<EmailNotification> query = ApplyFilter(
            dbContext.Set<EmailNotification>().AsNoTracking(),
            filter);

        long total = await query.LongCountAsync(cancellationToken);
        List<MailNotificationListItem> items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => ToListItem(x))
            .ToListAsync(cancellationToken);

        return new PageResponse<MailNotificationListItem>(items, safePage, safePageSize, total);
    }

    public async Task<MailNotificationDetail?> GetAsync(long id, CancellationToken cancellationToken)
    {
        EmailNotification? notification = await dbContext.Set<EmailNotification>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return notification is null ? null : ToDetail(notification);
    }

    public async Task<MailNotificationSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        DateTime todayUtc = clock.UtcNow.UtcDateTime.Date;
        IQueryable<EmailNotification> notifications = dbContext.Set<EmailNotification>().AsNoTracking();

        return new MailNotificationSummary(
            Pending: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.Pending, cancellationToken),
            Processing: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.Processing, cancellationToken),
            SentToday: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.Sent && x.SentAtUtc >= todayUtc, cancellationToken),
            FailedRetryable: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.FailedRetryable, cancellationToken),
            FailedFinal: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.FailedFinal, cancellationToken),
            Cancelled: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.Cancelled, cancellationToken),
            Suppressed: await notifications.LongCountAsync(x => x.StatusCode == EmailNotificationStatusCodes.Suppressed, cancellationToken));
    }

    public Task<Result<MailNotificationDetail>> RetryAsync(long id, CancellationToken cancellationToken)
        => MutateAsync(
            id,
            notification => notification.StatusCode is EmailNotificationStatusCodes.FailedRetryable
                or EmailNotificationStatusCodes.FailedFinal,
            notification => notification.ResetForRetry(clock.UtcNow.UtcDateTime),
            MailDeliveryErrors.NotificationCannotBeRetried,
            cancellationToken);

    public Task<Result<MailNotificationDetail>> CancelAsync(long id, CancellationToken cancellationToken)
        => MutateAsync(
            id,
            notification => notification.StatusCode is EmailNotificationStatusCodes.Pending
                or EmailNotificationStatusCodes.FailedRetryable,
            notification => notification.MarkCancelled(clock.UtcNow.UtcDateTime),
            MailDeliveryErrors.NotificationCannotBeCancelled,
            cancellationToken);

    public Task<Result<MailNotificationDetail>> SuppressAsync(
        long id,
        string? reason,
        CancellationToken cancellationToken)
        => MutateAsync(
            id,
            notification => notification.StatusCode is EmailNotificationStatusCodes.Pending
                or EmailNotificationStatusCodes.FailedRetryable
                or EmailNotificationStatusCodes.FailedFinal,
            notification => notification.MarkSuppressed(clock.UtcNow.UtcDateTime, reason),
            MailDeliveryErrors.NotificationCannotBeSuppressed,
            cancellationToken);

    private async Task<Result<MailNotificationDetail>> MutateAsync(
        long id,
        Func<EmailNotification, bool> canMutate,
        Action<EmailNotification> mutate,
        Error invalidStateError,
        CancellationToken cancellationToken)
    {
        EmailNotification? notification = await dbContext.Set<EmailNotification>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (notification is null)
        {
            return Result<MailNotificationDetail>.Failure(MailDeliveryErrors.NotificationNotFound);
        }

        if (!canMutate(notification))
        {
            return Result<MailNotificationDetail>.Failure(invalidStateError);
        }

        mutate(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<MailNotificationDetail>.Success(ToDetail(notification));
    }

    private static IQueryable<EmailNotification> ApplyFilter(
        IQueryable<EmailNotification> query,
        MailNotificationFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            string status = filter.Status.Trim();
            query = query.Where(x => x.StatusCode == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.NotificationType))
        {
            string notificationType = filter.NotificationType.Trim();
            query = query.Where(x => x.NotificationType == notificationType);
        }

        if (filter.PersonId.HasValue)
        {
            query = query.Where(x => x.PersonId == filter.PersonId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            string entityType = filter.EntityType.Trim();
            query = query.Where(x => x.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            string entityId = filter.EntityId.Trim();
            query = query.Where(x => x.EntityId == entityId);
        }

        if (filter.CreatedFromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= filter.CreatedFromUtc.Value);
        }

        if (filter.CreatedToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= filter.CreatedToUtc.Value);
        }

        return query;
    }

    private static MailNotificationListItem ToListItem(EmailNotification notification)
        => new(
            notification.Id,
            notification.NotificationType,
            notification.PersonId,
            notification.Subject,
            notification.EntityType,
            notification.EntityId,
            notification.StatusCode,
            notification.AttemptCount,
            notification.MaxAttempts,
            notification.NextAttemptAtUtc,
            notification.LockedUntilUtc,
            notification.LastErrorCode,
            notification.LastErrorMessage,
            notification.ResolvedToEmailMasked,
            notification.RecipientSourceCode,
            notification.CreatedAtUtc,
            notification.SentAtUtc,
            notification.CancelledAtUtc);

    private static MailNotificationDetail ToDetail(EmailNotification notification)
        => new(
            notification.Id,
            notification.NotificationType,
            notification.PersonId,
            notification.Subject,
            Preview(notification.PlainTextBody),
            !string.IsNullOrWhiteSpace(notification.HtmlBody),
            notification.EntityType,
            notification.EntityId,
            notification.StatusCode,
            notification.Priority,
            notification.AttemptCount,
            notification.MaxAttempts,
            notification.NextAttemptAtUtc,
            notification.LockedUntilUtc,
            notification.LastErrorCode,
            notification.LastErrorMessage,
            notification.ResolvedToEmailMasked,
            notification.RecipientSourceCode,
            notification.CreatedAtUtc,
            notification.SentAtUtc,
            notification.CancelledAtUtc);

    private static string Preview(string body)
    {
        string normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
