namespace Moe.Modules.AiCopilot.Application.Knowledge;

public sealed record KnowledgeCitation(string SourceId, string Title, string Section, string SourceStatus,
    string Version, DateOnly EffectiveDate, string? Url);
public sealed record KnowledgeSourceSummary(string SourceId, string Title, string SourceStatus,
    DateOnly EffectiveDate, string ReviewOwner, IReadOnlyCollection<string> AllowedIntents);
public sealed record KnowledgeResult(KnowledgeCitation Citation, string Content, double Score,
    IReadOnlyCollection<string> FollowUps, IReadOnlyCollection<string> AllowedIntents, string ReviewOwner);

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeResult>> RetrieveAsync(string query, string? domain, int limit = 4, CancellationToken ct = default);
}
