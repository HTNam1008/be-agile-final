using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.FasPayment.Application.Applications.Reject;

public sealed record RejectApplicationCommand(long ApplicationId, string RejectionReasonCode, string? Remarks) : ICommand<RejectApplicationResponse>;

public sealed record RejectApplicationResponse(long ApplicationId, string Status, string Decision, string RejectionReasonCode);
