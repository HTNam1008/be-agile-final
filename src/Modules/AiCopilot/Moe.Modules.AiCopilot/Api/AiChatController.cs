using Asp.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Orchestration;
using Moe.Modules.AiCopilot.Application.Security;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.AiCopilot.Api;

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/ai")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
public sealed class AiChatController(
    AiTurnRouter router,
    AiStreamingService streaming,
    MoeDbContext db,
    ICurrentUser currentUser,
    SensitiveDataRedactor redactor,
    ILogger<AiChatController> logger) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        try
        {
            AiChatResponse response = await router.ChatAsync(request, ct);
            return Ok(response);
        }
        catch (ConcurrencyConflictException)
        {
            HttpContext.Response.Headers.RetryAfter = "1";
            return Conflict(new { error = "AI.CONCURRENCY_CONFLICT", message = "This FAS session was modified by another request. Please retry.", retryAfter = "1" });
        }
    }

    [HttpPost("admin-center-cases")]
    public async Task<object> CreateCase(CreateAdminCenterCaseRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        var review = await db.Set<AiReviewRecord>()
            .SingleOrDefaultAsync(x => x.Id == request.ReviewRecordId && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.REVIEW_NOT_FOUND");
        var item = AdminCenterCase.Create(review.Id, personId, redactor.Redact(request.Description),
            request.ContactPreference, DateTime.UtcNow);
        db.Add(item); await db.SaveChangesAsync(ct);
        return new { caseId = item.Id, status = item.StatusCode, createdAtUtc = item.CreatedAtUtc };
    }

    [HttpPost("chat/stream")]
    public async Task Stream([FromBody] AiChatRequest request, CancellationToken ct)
    {
        _ = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");

        var sanitized = new AiChatRequest
        {
            ConversationId = request.ConversationId ?? Guid.NewGuid(),
            Message = request.Message,
            PageContext = AiRouterHelpers.SanitizePageContext(request.PageContext),
            FasState = request.FasState
        };

        HttpContext.Response.ContentType = "text/event-stream";
        HttpContext.Response.Headers.CacheControl = "no-cache";

        try
        {
            AiChatResponse response = await router.ChatAsync(sanitized, ct);
            await streaming.StreamResponseAsync(HttpContext, response, ct);
        }
        catch (ConcurrencyConflictException)
        {
            await streaming.WriteErrorEventAsync(HttpContext,
                "AI.CONCURRENCY_CONFLICT: This FAS session was modified by another request. Please retry.", ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — no SSE possible
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI stream conv {Id} failed", sanitized.ConversationId);
            try
            {
                await streaming.WriteErrorEventAsync(HttpContext,
                    "AI_STREAM_ERROR: Something went wrong. Please retry or use the Help links.", ct);
            }
            catch
            {
                // Response may already be committed
            }
        }
    }

}
