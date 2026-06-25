using Moe.Application.Abstractions.Messaging;
using Moe.Modules.Mfa.Application.GetMfaStatus;

namespace Moe.Modules.Mfa.Application.ChangePin;

public sealed record ChangeMfaPinCommand(string OldPin, string NewPin) : ICommand<MfaStatusResponse>;
