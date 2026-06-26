using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Reviews;

namespace Moe.Modules.AiCopilot.Api;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/ai/reviews")]
[Authorize(Policy = AuthorizationPolicies.ManageAiReviews)]
[EnableCors("AdminCors")]
public sealed class AiReviewsController(AiReviewService reviews) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? domain, [FromQuery] string? reason,
        [FromQuery] string? severity, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc, CancellationToken cancellationToken)
        => Ok(await reviews.List(domain, reason, severity, status, search, page, pageSize, fromUtc, toUtc, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        AiReviewDetail? review = await reviews.Get(id, cancellationToken);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken cancellationToken)
        => await reviews.Resolve(id, cancellationToken) ? NoContent() : NotFound();
}
