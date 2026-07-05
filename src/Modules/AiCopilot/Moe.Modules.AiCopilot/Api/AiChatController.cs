using System.Text.Json;
using Asp.Versioning;
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
    SensitiveDataRedactor redactor) : ControllerBase
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
        DateTime now = DateTime.UtcNow;

        var sanitized = new AiChatRequest
        {
            ConversationId = request.ConversationId ?? Guid.NewGuid(),
            Message = request.Message,
            PageContext = AiRouterHelpers.SanitizePageContext(request.PageContext)
        };

        AiConversation conversation = await db.Set<AiConversation>().Include(x => x.FasSession)
            .SingleOrDefaultAsync(x => x.Id == sanitized.ConversationId!.Value, ct)
            ?? AiConversation.Start(sanitized.ConversationId!.Value, personId, now);
        if (conversation.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");

        string pj = sanitized.PageContext is null ? null! : JsonSerializer.Serialize(sanitized.PageContext, JsonOptions);
        db.Add(AiMessage.Create(conversation.Id, "USER", redactor.Redact(sanitized.Message), now));

        await streaming.StreamResponseAsync(HttpContext, conversation, sanitized, ct);
    }

    private static AiInterviewState? DeserializeInterviewState(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var state = JsonSerializer.Deserialize<FasInterviewData>(json, JsonOptions);
        return state is null ? null : new AiInterviewState(state.Status, null, [], [], null);
    }
}
