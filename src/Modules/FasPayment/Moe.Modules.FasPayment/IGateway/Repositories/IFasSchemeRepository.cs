using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.IGateway.Repositories;

internal interface IFasSchemeRepository
{
    Task<bool> SchemeCodeExistsAsync(string schemeCode, CancellationToken cancellationToken);
    Task<bool> GrantCodeExistsAsync(string grantCode, CancellationToken cancellationToken);
    Task<bool> SchemeCodeExistsExcludingAsync(string schemeCode, long excludedSchemeId, CancellationToken cancellationToken)
        => SchemeCodeExistsAsync(schemeCode, cancellationToken);
    Task<bool> GrantCodeExistsExcludingAsync(string grantCode, long excludedSchemeId, CancellationToken cancellationToken)
        => GrantCodeExistsAsync(grantCode, cancellationToken);
    Task<CreateFasSchemeResponse> CreateAsync(CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken);
    Task<CreateFasSchemeResponse> SaveDraftAsync(long? schemeId, CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken);
    Task<CreateFasSchemeResponse> ActivateDraftAsync(long schemeId, CreateFasSchemeRequest request, long actorId, DateTime utcNow, CancellationToken cancellationToken);
    Task<bool> DeleteDraftAsync(long schemeId, CancellationToken cancellationToken);
    Task<FasSchemeListResponse> ListAsync(string? status, string? search, CancellationToken cancellationToken);
    Task<FasSchemeDetail?> GetAsync(long schemeId, CancellationToken cancellationToken);
}
