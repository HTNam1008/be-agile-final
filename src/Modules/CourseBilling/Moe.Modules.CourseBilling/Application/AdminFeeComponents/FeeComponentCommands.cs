using Moe.Application.Abstractions.Messaging;
using Moe.Modules.CourseBilling.Contracts.AdminFeeComponents;

namespace Moe.Modules.CourseBilling.Application.AdminFeeComponents;

public sealed record CreateFeeComponentCommand(CreateFeeComponentRequest Request) : ICommand<FeeComponentDto>;
public sealed record UpdateFeeComponentCommand(long FeeComponentId, UpdateFeeComponentRequest Request) : ICommand<FeeComponentDto>;
public sealed record ActivateFeeComponentCommand(long FeeComponentId) : ICommand<FeeComponentDto>;
public sealed record DeactivateFeeComponentCommand(long FeeComponentId) : ICommand<FeeComponentDto>;
public sealed record DeleteFeeComponentCommand(long FeeComponentId) : ICommand<long>;
