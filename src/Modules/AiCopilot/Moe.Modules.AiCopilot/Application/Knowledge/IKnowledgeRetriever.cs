namespace Moe.Modules.AiCopilot.Application.Knowledge;

public sealed record KnowledgeCitation(string SourceId, string Title, string Section, string SourceStatus,
    string Version, DateOnly EffectiveDate, string? Url);
public sealed record KnowledgeResult(KnowledgeCitation Citation, string Content, double Score);

public interface IKnowledgeRetriever
{
    IReadOnlyList<KnowledgeResult> Retrieve(string query, string? domain, int limit = 4);
}
