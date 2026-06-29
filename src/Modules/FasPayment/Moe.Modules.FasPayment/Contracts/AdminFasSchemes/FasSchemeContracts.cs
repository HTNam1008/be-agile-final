namespace Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

public sealed record ListFasSchemesRequest(
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 10,
    string? SortBy = null,
    string? SortDirection = null,
    DateOnly? DurationFrom = null,
    DateOnly? DurationTo = null);

public sealed record CreateFasSchemeRequest(
    string SchemeCode, string GrantCode, string Name, string? Description,
    DateOnly StartDate, DateOnly EndDate, IReadOnlyList<long> CourseIds,
    string SubsidyType, IReadOnlyList<FasCriteriaTemplateItem> CriteriaTemplate,
    IReadOnlyList<CreateFasTierRequest> Tiers);

public sealed record FasCriteriaTemplateItem(string CriteriaType, string? ConnectorToNext, int DisplayOrder);

public sealed record CreateFasTierRequest(
    string Label, decimal SubsidyValue, int DisplayOrder,
    IReadOnlyList<FasTierCriteriaValue> CriteriaValues,
    string? GrantCode = null, string? SubsidyType = null);

public sealed record FasTierCriteriaValue(
    int DisplayOrder, decimal? NumberFrom, decimal? NumberTo,
    IReadOnlyList<string>? Nationalities);

public sealed record CreateFasSchemeResponse(long SchemeId, string SchemeCode, string GrantCode, string Status);
public sealed record FasSchemeListItem(long SchemeId, string SchemeCode, string GrantCode, string Name,
    string? Description, DateOnly StartDate, DateOnly EndDate, DateTime CreatedAtUtc, string Status, IReadOnlyList<long> CourseIds,
    int ApplicationCount);
public sealed record FasSchemeDetail(long SchemeId, string SchemeCode, string GrantCode, string Name,
    string? Description, DateOnly StartDate, DateOnly EndDate, string Status, IReadOnlyList<long> CourseIds,
    string SubsidyType, IReadOnlyList<FasCriteriaTemplateItem> CriteriaTemplate, IReadOnlyList<FasTierDetail> Tiers);
public sealed record FasTierDetail(long TierId, string Label, decimal SubsidyValue, int DisplayOrder,
    IReadOnlyList<FasTierCriteriaValue> CriteriaValues);
