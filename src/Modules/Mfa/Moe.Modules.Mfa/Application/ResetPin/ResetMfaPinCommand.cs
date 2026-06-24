using Moe.Application.Abstractions.Messaging;
using Moe.Modules.Mfa.Application.GetMfaStatus;

namespace Moe.Modules.Mfa.Application.ResetPin;

public sealed record ResetMfaPinCommand(long LoginAccountId, string Reason) : ICommand<MfaStatusResponse>;
