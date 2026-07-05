using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    [HttpGet("conversations/{id:guid}")]
    public async Task<AiConversationResponse> Conversation(Guid id, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException();
        var conversation = await db.Set<AiConversation>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.PersonId == personId, ct)
            ?? throw new KeyNotFoundException("AI.CONVERSATION_NOT_FOUND");
        var messages = await db.Set<AiMessage>().AsNoTracking().Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new AiConversationMessageResponse(x.Id, x.RoleCode, x.Content, x.CreatedAtUtc,
                x.ResponseJson == null ? null : JsonSerializer.Deserialize<object>(x.ResponseJson, JsonOptions)))
            .ToArrayAsync(ct);
        return new(conversation.Id, conversation.ModeCode, conversation.StatusCode, messages,
            DeserializeInterviewState(conversation.FasSession?.CollectedFactsJson));
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
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");

        var sanitized = new AiChatRequest
        {
            ConversationId = request.ConversationId ?? Guid.NewGuid(),
            Message = request.Message,
            PageContext = AiRouterHelpers.SanitizePageContext(request.PageContext)
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

    private static AiInterviewState? DeserializeInterviewState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var state = JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions);
        return state is null ? null : new AiInterviewState(state.Status, null, [], [], null);
    }
}
