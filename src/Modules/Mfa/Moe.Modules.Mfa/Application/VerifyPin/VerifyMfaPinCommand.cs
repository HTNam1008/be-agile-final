using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.VerifyPin;

public sealed record VerifyMfaPinCommand(Guid ChallengeId, string Pin) : ICommand<MfaVerificationResponse>;
