using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Orchestration;

namespace Moe.Modules.AiCopilot.Api;

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/ai")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
public sealed class AiChatController(AiOrchestratorService service) : ControllerBase
{
    [HttpPost("chat")]
    public Task<AiChatResponse> Chat([FromBody] AiChatRequest request, CancellationToken ct) => service.ChatAsync(request, ct);

    [HttpGet("conversations/{id:guid}")]
    public Task<AiConversationResponse> Conversation(Guid id, CancellationToken ct) => service.GetConversationAsync(id, ct);

    [HttpPost("admin-center-cases")]
    public Task<object> CreateCase(CreateAdminCenterCaseRequest request, CancellationToken ct) => service.CreateCaseAsync(request, ct);
}
