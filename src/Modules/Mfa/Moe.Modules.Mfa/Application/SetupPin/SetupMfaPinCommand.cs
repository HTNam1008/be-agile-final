using Moe.Application.Abstractions.Messaging;
using Moe.Modules.Mfa.Application.GetMfaStatus;

namespace Moe.Modules.Mfa.Application.SetupPin;

public sealed record SetupMfaPinCommand(string Pin) : ICommand<MfaStatusResponse>;
