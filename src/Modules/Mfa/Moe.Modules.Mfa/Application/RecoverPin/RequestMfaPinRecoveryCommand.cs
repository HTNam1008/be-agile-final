using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.RecoverPin;

public sealed record RequestMfaPinRecoveryCommand : ICommand<bool>;
