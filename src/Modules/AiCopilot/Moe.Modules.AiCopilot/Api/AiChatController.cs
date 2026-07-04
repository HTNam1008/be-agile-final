using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Orchestration;
using Moe.Modules.AiCopilot.Domain;
using Moe.StudentFinance.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Moe.Modules.AiCopilot.Api;

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/ai")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
public sealed class AiChatController(
    AiTurnRouter router,
    AiStreamingService streaming,
    MoeDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("chat")]
    public Task<AiChatResponse> Chat([FromBody] AiChatRequest request, CancellationToken ct) => router.ChatAsync(request, ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<AiConversationResponse> Conversation(Guid id, CancellationToken ct) => router.GetConversationAsync(id, ct);

    [HttpPost("admin-center-cases")]
    public Task<object> CreateCase(CreateAdminCenterCaseRequest request, CancellationToken ct) => router.CreateCaseAsync(request, ct);

    [HttpPost("chat/stream")]
    public async Task Stream([FromBody] AiChatRequest request, CancellationToken ct)
    {
        long personId = currentUser.PersonId ?? throw new UnauthorizedAccessException("AI.AUTHENTICATION_REQUIRED");
        DateTime now = DateTime.UtcNow;
        AiConversation conversation = await db.Set<AiConversation>().Include(x => x.FasSession)
            .SingleOrDefaultAsync(x => x.Id == request.ConversationId!.Value, ct)
            ?? AiConversation.Start(request.ConversationId ?? Guid.NewGuid(), personId, now);
        if (conversation.PersonId != personId) throw new UnauthorizedAccessException("AI.CONVERSATION_FORBIDDEN");

        await streaming.StreamResponseAsync(HttpContext, conversation, request, ct);
    }
}
