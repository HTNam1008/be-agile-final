using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.Modules.Mfa.IGateway.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.RecoverPin;

internal sealed class CompleteMfaPinRecoveryHandler(
    IMfaChallengeRepository challenges, IMfaCredentialRepository credentials,
    IMfaAuditEventRepository auditEvents, IMfaPinHasher pinHasher, IClock clock)
    : ICommandHandler<CompleteMfaPinRecoveryCommand, bool>
{
    public async Task<Result<bool>> Handle(CompleteMfaPinRecoveryCommand command, CancellationToken cancellationToken)
    {
        if (!PinRules.IsValid(command.Pin)) return Result<bool>.Failure(MfaErrors.InvalidPinFormat);
        if (!Guid.TryParse(command.Token, out Guid id)) return Result<bool>.Failure(MfaErrors.RecoveryLinkInvalid);
        LoginMfaChallenge? challenge = await challenges.FindByIdAsync(id, cancellationToken);
        if (challenge is null || challenge.PurposeCode != MfaChallengePurposeCodes.Recovery || challenge.StatusCode != MfaChallengeStatusCodes.Pending)
            return Result<bool>.Failure(MfaErrors.RecoveryLinkInvalid);

        DateTime now = clock.UtcNow.UtcDateTime;
        if (challenge.IsExpired(now))
        {
            challenge.MarkExpired();
            await challenges.SaveChangesAsync(cancellationToken);
            return Result<bool>.Failure(MfaErrors.RecoveryLinkExpired);
        }

        LoginMfaCredential? credential = await credentials.FindPinAsync(challenge.LoginAccountId, cancellationToken);
        if (credential is null) return Result<bool>.Failure(MfaErrors.PinNotConfigured);
        MfaPinHash hash = pinHasher.Hash(command.Pin);
        credential.ReplaceSecret(hash.Hash, hash.Salt, hash.Algorithm, now);
        challenge.MarkVerified(now);
        auditEvents.Add(challenge.LoginAccountId, challenge.Id, MfaAuditEventCodes.RecoveryCompleted);
        await challenges.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
