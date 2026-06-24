using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.StartChallenge;

public sealed record StartMfaChallengeCommand : ICommand<MfaChallengeResponse>;
