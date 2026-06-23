using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;

namespace Moe.Modules.FasPayment.Application.AdminFasSchemes;

internal sealed record CreateFasSchemeCommand(CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record SaveFasSchemeDraftCommand(long? SchemeId, CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record ActivateFasSchemeDraftCommand(long SchemeId, CreateFasSchemeRequest Request) : ICommand<CreateFasSchemeResponse>;
internal sealed record DeleteFasSchemeDraftCommand(long SchemeId) : ICommand<bool>;
internal sealed record ListFasSchemesQuery(string? Status, string? Search) : IQuery<FasSchemeListResponse>;
internal sealed record GetFasSchemeQuery(long SchemeId) : IQuery<FasSchemeDetail>;
