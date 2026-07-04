using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Api;

[ApiController]
[ApiVersion(1.0)]
[Route("api/admin/v{version:apiVersion}/ai/knowledge")]
[Authorize(Policy = AuthorizationPolicies.ManageAiReviews)]
[EnableCors("AdminCors")]
public sealed class AiKnowledgeAdminController(IKnowledgeDocumentStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? domain, CancellationToken ct)
    {
        IReadOnlyList<KnowledgeDocument> docs = domain is not null
            ? await store.GetByDomainAsync(domain, ct)
            : await store.GetAllAsync(ct);
        return Ok(docs.Select(d => new
        {
            d.Id, d.Title, d.Section, d.Domain, d.Status, d.Version,
            effectiveDate = d.EffectiveDate.ToString("O"),
            d.Url, d.AlwaysInclude, d.ReviewOwner
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        IReadOnlyList<KnowledgeDocument> docs = await store.GetAllAsync(ct);
        KnowledgeDocument? doc = docs.FirstOrDefault(d => d.Id == id);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Upsert(string id, [FromBody] UpsertKnowledgeRequest request, CancellationToken ct)
    {
        var doc = new KnowledgeDocument(
            id, request.Title, request.Section ?? request.Title,
            request.Domain, request.Status, request.Version ?? "1.0",
            DateOnly.TryParse(request.EffectiveDate, out DateOnly ed) ? ed : DateOnly.FromDateTime(DateTime.UtcNow),
            request.Content, request.Url, request.Synonyms, request.AlwaysInclude,
            request.ReviewOwner ?? "Admin", request.AllowedIntents, request.FollowUps);
        await store.UpsertAsync(doc, ct);
        return Ok(new { id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await store.DeleteAsync(id, ct);
        return NoContent();
    }
}

public sealed record UpsertKnowledgeRequest(
    string Title, string Domain, string Content, string Status,
    string? Section = null, string? Version = null, string? EffectiveDate = null,
    string? Url = null, string[]? Synonyms = null, bool AlwaysInclude = false,
    string? ReviewOwner = null, string[]? AllowedIntents = null, string[]? FollowUps = null);
