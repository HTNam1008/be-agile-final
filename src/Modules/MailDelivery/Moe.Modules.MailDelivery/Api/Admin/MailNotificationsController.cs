using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.MailDelivery.Application.Admin;
using Moe.Modules.MailDelivery.Domain;
using Moe.SharedKernel.Results;

namespace Moe.Modules.MailDelivery.Api.Admin;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/mail-notifications")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
[EnableCors("AdminCors")]
public sealed class MailNotificationsController(
    IMailNotificationAdminService mailNotifications) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<MailNotificationSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        MailNotificationSummary summary = await mailNotifications.GetSummaryAsync(cancellationToken);
        return ApiResponseFactory.Ok(summary, HttpContext.TraceIdentifier, "Mail notification summary retrieved.");
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PageResponse<MailNotificationListItem>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] MailNotificationListRequest request,
        CancellationToken cancellationToken)
    {
        MailNotificationFilter filter = new(
            request.Search,
            request.Status,
            request.NotificationType,
            request.PersonId,
            request.EntityType,
            request.EntityId,
            request.CreatedFromUtc,
            request.CreatedToUtc,
            request.SortBy,
            request.SortDirection);

        PageResponse<MailNotificationListItem> page = await mailNotifications.ListAsync(
            filter,
            request.Page,
            request.PageSize,
            cancellationToken);

        return ApiResponseFactory.Ok(page, HttpContext.TraceIdentifier, "Mail notifications retrieved.");
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<MailNotificationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        MailNotificationDetail? detail = await mailNotifications.GetAsync(id, cancellationToken);
        return detail is null
            ? Failure(MailDeliveryErrors.NotificationNotFound)
            : ApiResponseFactory.Ok(detail, HttpContext.TraceIdentifier, "Mail notification retrieved.");
    }

    [HttpPost("{id:long}/retry")]
    [ProducesResponseType(typeof(ApiResponse<MailNotificationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(long id, CancellationToken cancellationToken)
    {
        Result<MailNotificationDetail> result = await mailNotifications.RetryAsync(id, cancellationToken);
        return ToActionResponse(result, "Mail notification queued for retry.");
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<MailNotificationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        Result<MailNotificationDetail> result = await mailNotifications.CancelAsync(id, cancellationToken);
        return ToActionResponse(result, "Mail notification cancelled.");
    }

    [HttpPost("{id:long}/suppress")]
    [ProducesResponseType(typeof(ApiResponse<MailNotificationDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Suppress(
        long id,
        [FromBody] SuppressMailNotificationRequest? request,
        CancellationToken cancellationToken)
    {
        Result<MailNotificationDetail> result = await mailNotifications.SuppressAsync(
            id,
            request?.Reason,
            cancellationToken);
        return ToActionResponse(result, "Mail notification suppressed.");
    }

    private IActionResult ToActionResponse(Result<MailNotificationDetail> result, string message)
        => result.IsSuccess
            ? ApiResponseFactory.Ok(result.Value, HttpContext.TraceIdentifier, message)
            : Failure(result.Error);

    private ObjectResult Failure(Error error)
        => ApiResponseFactory.Failure(
            error,
            error == MailDeliveryErrors.NotificationNotFound
                ? ApiResponseCodes.NotFound
                : ApiResponseCodes.Conflict,
            HttpContext.TraceIdentifier);
}

public sealed class MailNotificationListRequest
{
    public string? Search { get; init; }

    public string? Status { get; init; }

    public string? NotificationType { get; init; }

    public long? PersonId { get; init; }

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public DateTime? CreatedFromUtc { get; init; }

    public DateTime? CreatedToUtc { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

public sealed record SuppressMailNotificationRequest(string? Reason);
