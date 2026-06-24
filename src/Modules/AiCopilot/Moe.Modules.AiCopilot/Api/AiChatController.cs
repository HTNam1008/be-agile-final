using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Orchestration;

namespace Moe.Modules.AiCopilot.Api;

public sealed class AiConversationMessage
{
    [Required, RegularExpression("user|assistant")]
    public string Role { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Content { get; init; } = string.Empty;
}

public sealed record AiPageContext(string? Domain, string? Surface, string? Path, JsonElement? Entity);

public sealed class AiChatRequest
{
    public Guid? ConversationId { get; init; }

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    [MaxLength(12)]
    public IReadOnlyList<AiConversationMessage> Messages { get; init; } = [];

    public AiPageContext? PageContext { get; init; }
}

public sealed record AiChatResponse(
    Guid ConversationId,
    string Message,
    bool Grounded,
    IReadOnlyList<string> Capabilities);

[ApiController, ApiVersion(1.0)]
[Route("api/eservice/v{version:apiVersion}/ai")]
[Authorize(Policy = AuthorizationPolicies.EServicePortal), EnableCors("EServiceCors")]
public sealed class AiChatController(AiOrchestratorService aiService) : ControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType<AiChatResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiChatResponse>> Chat([FromBody] AiChatRequest request, CancellationToken ct)
    {
        AiChatResponse response = await aiService.ChatAsync(request, ct);
        return Ok(response);
    }
}
