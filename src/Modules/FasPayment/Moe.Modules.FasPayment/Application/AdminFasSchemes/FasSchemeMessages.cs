using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal sealed record CreateFasSchemeCommand(CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record SaveFasSchemeDraftCommand(long? SchemeId, CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record ActivateFasSchemeDraftCommand(long SchemeId, CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record DeleteFasSchemeDraftCommand(long SchemeId) : ICommand<bool>;
internal sealed record PublishFasSchemeCommand(long SchemeId) : ICommand<CreateFasSchemeResponse>;
internal sealed record DisableFasSchemeCommand(long SchemeId) : ICommand<CreateFasSchemeResponse>;
internal sealed record DeleteFasSchemeCommand(long SchemeId) : ICommand<CreateFasSchemeResponse>;
internal sealed record ListFasSchemesQuery(
    string? Status,
    string? Search,
    int Page,
    int PageSize,
    string? SortBy,
    string? SortDirection,
    DateOnly? DurationFrom,
    DateOnly? DurationTo) : IQuery<PageResponse<FasSchemeListItem>>;
internal sealed record GetFasSchemeQuery(long SchemeId) : IQuery<FasSchemeDetail>;
