using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.FasPayment.Application.Applications.Approve;

public sealed record ApproveApplicationCommand(long ApplicationId, string? Remarks) : ICommand<ApproveApplicationResponse>;

public sealed record ApproveApplicationResponse(long ApplicationId, string Status, string Decision);
