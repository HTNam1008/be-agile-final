using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.RecoverPin;

public sealed record CompleteMfaPinRecoveryCommand(string Token, string Pin) : ICommand<bool>;
