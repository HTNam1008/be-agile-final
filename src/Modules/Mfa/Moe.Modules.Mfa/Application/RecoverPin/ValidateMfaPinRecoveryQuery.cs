using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.RecoverPin;

public sealed record ValidateMfaPinRecoveryQuery(string Token) : IQuery<MfaPinRecoveryTokenStatus>;

public sealed record MfaPinRecoveryTokenStatus(bool IsValid, string Status);
