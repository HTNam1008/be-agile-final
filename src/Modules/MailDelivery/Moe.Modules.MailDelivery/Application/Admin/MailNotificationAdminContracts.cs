using Moe.Infrastructure.Shared.Api;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Application.Admin;

public sealed record MailNotificationFilter(
    string? Search,
    string? Status,
    string? NotificationType,
    long? PersonId,
    string? EntityType,
    string? EntityId,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    string? SortBy,
    string? SortDirection);

public sealed record MailNotificationListItem(
    long Id,
    string NotificationType,
    long PersonId,
    string Subject,
    string? EntityType,
    string? EntityId,
    string StatusCode,
    int AttemptCount,
    int MaxAttempts,
    DateTime NextAttemptAtUtc,
    DateTime? LockedUntilUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    string? ResolvedToEmailMasked,
    string? RecipientSourceCode,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? CancelledAtUtc);

public sealed record MailNotificationDetail(
    long Id,
    string NotificationType,
    long PersonId,
    string Subject,
    string PlainTextPreview,
    bool HasHtmlBody,
    string? EntityType,
    string? EntityId,
    string StatusCode,
    int Priority,
    int AttemptCount,
    int MaxAttempts,
    DateTime NextAttemptAtUtc,
    DateTime? LockedUntilUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    string? ResolvedToEmailMasked,
    string? RecipientSourceCode,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? CancelledAtUtc);

public sealed record MailNotificationSummary(
    long Pending,
    long Processing,
    long SentToday,
    long FailedRetryable,
    long FailedFinal,
    long Cancelled,
    long Suppressed);

public interface IMailNotificationAdminService
{
    Task<PageResponse<MailNotificationListItem>> ListAsync(
        MailNotificationFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MailNotificationDetail?> GetAsync(long id, CancellationToken cancellationToken);

    Task<MailNotificationSummary> GetSummaryAsync(CancellationToken cancellationToken);

    Task<Result<MailNotificationDetail>> RetryAsync(long id, CancellationToken cancellationToken);

    Task<Result<MailNotificationDetail>> CancelAsync(long id, CancellationToken cancellationToken);

    Task<Result<MailNotificationDetail>> SuppressAsync(
        long id,
        string? reason,
        CancellationToken cancellationToken);
}
