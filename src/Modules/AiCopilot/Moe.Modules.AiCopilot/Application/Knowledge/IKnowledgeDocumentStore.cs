namespace Moe.Modules.AiCopilot.Application.Knowledge;

public interface IKnowledgeDocumentStore
{
    Task<IReadOnlyList<KnowledgeDocument>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<KnowledgeDocument>> GetByDomainAsync(string domain, CancellationToken ct);
    Task UpsertAsync(KnowledgeDocument document, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<KnowledgeDocument>> SearchAsync(string query, int limit = 5, string? domain = null, CancellationToken ct = default);
}
