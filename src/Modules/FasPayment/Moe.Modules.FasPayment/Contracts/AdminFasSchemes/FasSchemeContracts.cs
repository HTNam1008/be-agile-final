namespace Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

public sealed record ListFasSchemesRequest(string? Status = null, string? Search = null);

public sealed record CreateFasSchemeRequest(
    string SchemeCode, string GrantCode, string Name, string? Description,
    DateOnly StartDate, DateOnly EndDate, IReadOnlyList<long> CourseIds,
    IReadOnlyList<CreateFasTierRequest> Tiers);

public sealed record CreateFasTierRequest(
    string Label, string SubsidyType, decimal SubsidyValue, int DisplayOrder,
    IReadOnlyList<FasTierCriteriaRequest> Criteria);

public sealed record FasTierCriteriaRequest(
    string CriteriaType, decimal? NumberFrom, decimal? NumberTo,
    IReadOnlyList<string>? Nationalities, string? ConnectorToNext, int DisplayOrder);

public sealed record CreateFasSchemeResponse(long SchemeId, string SchemeCode, string GrantCode, string Status);
public sealed record FasSchemeListResponse(IReadOnlyList<FasSchemeListItem> Items);
public sealed record FasSchemeListItem(long SchemeId, string SchemeCode, string GrantCode, string Name,
    string? Description, DateOnly StartDate, DateOnly EndDate, string Status, IReadOnlyList<long> CourseIds);
public sealed record FasSchemeDetail(long SchemeId, string SchemeCode, string GrantCode, string Name,
    string? Description, DateOnly StartDate, DateOnly EndDate, string Status, IReadOnlyList<long> CourseIds,
    IReadOnlyList<FasTierDetail> Tiers);
public sealed record FasTierDetail(long TierId, string Label, string SubsidyType, decimal SubsidyValue, int DisplayOrder,
    IReadOnlyList<FasTierCriteriaDetail> Criteria);
public sealed record FasTierCriteriaDetail(long CriteriaId, string CriteriaType, decimal? NumberFrom, decimal? NumberTo,
    IReadOnlyList<string>? Nationalities, string? ConnectorToNext, int DisplayOrder);
