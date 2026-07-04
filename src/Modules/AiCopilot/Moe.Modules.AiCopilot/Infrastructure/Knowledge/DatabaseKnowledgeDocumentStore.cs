using Moe.Modules.AiCopilot.Application.Knowledge;

namespace Moe.Modules.AiCopilot.Infrastructure.Knowledge;

public sealed class DatabaseKnowledgeDocumentStore : IKnowledgeDocumentStore
{
    public Task<IReadOnlyList<KnowledgeDocument>> GetAllAsync(CancellationToken ct)
        => throw new NotImplementedException("DatabaseKnowledgeDocumentStore requires a database migration. Use EmbeddedKnowledgeDocumentStore.");

    public Task<IReadOnlyList<KnowledgeDocument>> GetByDomainAsync(string domain, CancellationToken ct)
        => throw new NotImplementedException("DatabaseKnowledgeDocumentStore requires a database migration. Use EmbeddedKnowledgeDocumentStore.");

    public Task UpsertAsync(KnowledgeDocument document, CancellationToken ct)
        => throw new NotImplementedException("DatabaseKnowledgeDocumentStore requires a database migration. Use EmbeddedKnowledgeDocumentStore.");

    public Task DeleteAsync(string id, CancellationToken ct)
        => throw new NotImplementedException("DatabaseKnowledgeDocumentStore requires a database migration. Use EmbeddedKnowledgeDocumentStore.");
}
