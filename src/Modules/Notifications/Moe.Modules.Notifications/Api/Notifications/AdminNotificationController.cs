using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.Notifications.Application.GetMyNotifications;
using Moe.Modules.Notifications.Application.GetUnreadNotificationCount;
using Moe.Modules.Notifications.Application.MarkNotificationAsRead;
using Moe.Modules.Notifications.IGateway.Notifications;

namespace Moe.Modules.Notifications.Api.Notifications;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/me/notifications")]
[EnableCors("PortalCors")]
[Authorize(Policy = AuthorizationPolicies.AdminPortal)]
public sealed class AdminNotificationController(
    IQueryDispatcher queries,
    ICommandDispatcher commands,
    INotificationWriter notificationWriter) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NotificationCreateRequest request, CancellationToken cancellationToken = default)
    {
        var result = await notificationWriter.CreateAsync(request, cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.BadRequest);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var result = await queries.Send(new GetMyNotificationsQuery(take), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var result = await queries.Send(new GetUnreadNotificationCountQuery(), cancellationToken);
        return result.ToApiResponse(this, ApiResponseCodes.Unauthorized);
    }

    [HttpPatch("{notificationId:long}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] long notificationId, CancellationToken cancellationToken = default)
    {
        var result = await commands.Send(new MarkNotificationAsReadCommand(notificationId), cancellationToken);
        return result.IsFailure
            ? ApiResponseFactory.Failure(result.Error, ApiResponseCodes.Unauthorized, HttpContext.TraceIdentifier)
            : ApiResponseFactory.NoContent(HttpContext.TraceIdentifier);
    }
}
