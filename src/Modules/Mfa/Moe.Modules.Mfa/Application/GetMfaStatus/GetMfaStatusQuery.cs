using Moe.Application.Abstractions.Messaging;

namespace Moe.Modules.Mfa.Application.GetMfaStatus;

public sealed record GetMfaStatusQuery : IQuery<MfaStatusResponse>;
