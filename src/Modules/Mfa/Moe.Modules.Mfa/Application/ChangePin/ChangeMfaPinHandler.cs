using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.Mfa.Application.GetMfaStatus;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.Modules.Mfa.IGateway.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.ChangePin;

internal sealed class ChangeMfaPinHandler(
    IMfaCredentialRepository credentials,
    IMfaAuditEventRepository auditEvents,
    ICurrentUser currentUser,
    IMfaPinHasher pinHasher,
    IClock clock,
    IOptions<MfaOptions> options) : ICommandHandler<ChangeMfaPinCommand, MfaStatusResponse>
{
    public async Task<Result<MfaStatusResponse>> Handle(
        ChangeMfaPinCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        if (!PinRules.IsValid(command.OldPin) || !PinRules.IsValid(command.NewPin))
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.InvalidPinFormat);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        LoginMfaCredential? credential = await credentials.FindActivePinAsync(
            currentUser.UserAccountId.Value,
            cancellationToken);

        if (credential is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.PinNotConfigured);
        }

        if (credential.IsLocked(utcNow))
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.CredentialLocked);
        }

        bool oldPinVerified = pinHasher.Verify(command.OldPin, credential.SecretSalt, credential.SecretHash);

        if (!oldPinVerified)
        {
            bool locked = credential.RecordFailedAttempt(
                options.Value.MaxFailedAttempts,
                TimeSpan.FromMinutes(options.Value.LockoutMinutes),
                utcNow);

            auditEvents.Add(
                credential.LoginAccountId,
                null,
                locked ? MfaAuditEventCodes.Locked : MfaAuditEventCodes.VerifyFailed);

            await credentials.SaveChangesAsync(cancellationToken);
            return Result<MfaStatusResponse>.Failure(locked ? MfaErrors.CredentialLocked : MfaErrors.InvalidPin);
        }

        MfaPinHash pinHash = pinHasher.Hash(command.NewPin);
        credential.ReplaceSecret(pinHash.Hash, pinHash.Salt, pinHash.Algorithm, utcNow);
        auditEvents.Add(credential.LoginAccountId, null, MfaAuditEventCodes.PinChanged);

        return Result<MfaStatusResponse>.Success(new MfaStatusResponse(
            credential.StatusCode,
            credential.LockedUntilUtc,
            credential.LastVerifiedAtUtc));
    }
}
