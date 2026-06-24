using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.Modules.Mfa.IGateway.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.VerifyPin;

internal sealed class VerifyMfaPinHandler(
    IMfaCredentialRepository credentials,
    IMfaChallengeRepository challenges,
    IMfaAuditEventRepository auditEvents,
    ICurrentUser currentUser,
    IMfaPinHasher pinHasher,
    IClock clock,
    IOptions<MfaOptions> options) : ICommandHandler<VerifyMfaPinCommand, MfaVerificationResponse>
{
    public async Task<Result<MfaVerificationResponse>> Handle(
        VerifyMfaPinCommand command,
        CancellationToken cancellationToken)
    {
        if (!PinRules.IsValid(command.Pin))
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.InvalidPinFormat);
        }

        if (currentUser.UserAccountId is null)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        LoginMfaChallenge? challenge = await challenges.FindByIdAsync(command.ChallengeId, cancellationToken);

        Result<MfaVerificationResponse>? challengeFailure = ValidateVerifyChallenge(challenge, utcNow);

        if (challengeFailure is not null)
        {
            await challenges.SaveChangesAsync(cancellationToken);
            return challengeFailure;
        }

        LoginMfaChallenge pendingChallenge = challenge!;

        if (pendingChallenge.LoginAccountId != currentUser.UserAccountId.Value)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.ChallengeNotFound);
        }

        LoginMfaCredential? credential = await credentials.FindActivePinAsync(
            pendingChallenge.LoginAccountId,
            cancellationToken);

        if (credential is null)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.PinNotConfigured);
        }

        if (credential.IsLocked(utcNow))
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.CredentialLocked);
        }

        bool verified = pinHasher.Verify(command.Pin, credential.SecretSalt, credential.SecretHash);

        if (!verified)
        {
            bool locked = credential.RecordFailedAttempt(
                options.Value.MaxFailedAttempts,
                TimeSpan.FromMinutes(options.Value.LockoutMinutes),
                utcNow);

            pendingChallenge.MarkFailed();
            auditEvents.Add(
                pendingChallenge.LoginAccountId,
                pendingChallenge.Id,
                locked ? MfaAuditEventCodes.Locked : MfaAuditEventCodes.VerifyFailed);

            await credentials.SaveChangesAsync(cancellationToken);
            return Result<MfaVerificationResponse>.Failure(locked ? MfaErrors.CredentialLocked : MfaErrors.InvalidPin);
        }

        credential.RecordVerified(utcNow);
        pendingChallenge.MarkVerified(utcNow);
        auditEvents.Add(
            pendingChallenge.LoginAccountId,
            pendingChallenge.Id,
            MfaAuditEventCodes.VerifySuccess);

        return Result<MfaVerificationResponse>.Success(new MfaVerificationResponse(
            true,
            pendingChallenge.LoginAccountId,
            pendingChallenge.PurposeCode,
            pendingChallenge.VerifiedAtUtc));
    }

    private static Result<MfaVerificationResponse>? ValidateVerifyChallenge(LoginMfaChallenge? challenge, DateTime utcNow)
    {
        if (challenge is null)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.ChallengeNotFound);
        }

        if (challenge.StatusCode is not MfaChallengeStatusCodes.Pending and not MfaChallengeStatusCodes.Failed)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.ChallengeNotPending);
        }

        if (challenge.PurposeCode != MfaChallengePurposeCodes.Login)
        {
            return Result<MfaVerificationResponse>.Failure(MfaErrors.PinNotConfigured);
        }

        if (!challenge.IsExpired(utcNow))
        {
            return null;
        }

        challenge.MarkExpired();
        return Result<MfaVerificationResponse>.Failure(MfaErrors.ChallengeExpired);
    }
}
