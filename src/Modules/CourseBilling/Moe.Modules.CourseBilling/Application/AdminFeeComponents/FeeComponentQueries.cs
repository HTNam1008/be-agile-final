using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Api;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

public sealed record ListFeeComponentsQuery(FeeComponentQueryRequest Request) : IQuery<PageResponse<FeeComponentDto>>;
public sealed record GetFeeComponentQuery(long FeeComponentId) : IQuery<FeeComponentDto>;
