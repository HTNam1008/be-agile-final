namespace Moe.Modules.Mfa.Api;

public sealed record CompleteMfaPinRecoveryRequest(string Token, string Pin);
