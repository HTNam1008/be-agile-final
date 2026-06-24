using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Orchestration;

namespace Moe.Modules.AiCopilot.Api;

public class AiChatRequest
{
    public string Message { get; set; } = string.Empty;
    public object? PageContext { get; set; }
}

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/ai")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
public sealed class AiChatController(AiOrchestratorService aiService) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        var response = await aiService.ChatAsync(request.Message, request.PageContext ?? new { }, ct);
        return Ok(new { message = response });
    }
}
