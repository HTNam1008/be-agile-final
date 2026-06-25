namespace Moe.Modules.Mfa.Api;

public sealed record ChangeMfaPinRequest(string OldPin, string NewPin);
