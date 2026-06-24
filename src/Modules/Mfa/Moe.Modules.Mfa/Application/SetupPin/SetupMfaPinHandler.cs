using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.Mfa.Application.GetMfaStatus;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.Modules.Mfa.IGateway.Security;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.SetupPin;

internal sealed class SetupMfaPinHandler(
    IMfaCredentialRepository credentials,
    IMfaAuditEventRepository auditEvents,
    ICurrentUser currentUser,
    IMfaPinHasher pinHasher,
    IClock clock) : ICommandHandler<SetupMfaPinCommand, MfaStatusResponse>
{
    public async Task<Result<MfaStatusResponse>> Handle(
        SetupMfaPinCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        if (!PinRules.IsValid(command.Pin))
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.InvalidPinFormat);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        LoginMfaCredential? existingCredential = await credentials.FindPinAsync(
            currentUser.UserAccountId.Value,
            cancellationToken);

        if (existingCredential is not null && existingCredential.StatusCode != MfaCredentialStatusCodes.ResetRequired)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.PinAlreadyConfigured);
        }

        MfaPinHash pinHash = pinHasher.Hash(command.Pin);
        LoginMfaCredential credential;
        if (existingCredential is null)
        {
            credential = LoginMfaCredential.CreatePin(
                currentUser.UserAccountId.Value,
                pinHash.Hash,
                pinHash.Salt,
                pinHash.Algorithm,
                utcNow);

            credentials.Add(credential);
        }
        else
        {
            credential = existingCredential;
            credential.ReplaceSecret(pinHash.Hash, pinHash.Salt, pinHash.Algorithm, utcNow);
        }

        auditEvents.Add(
            currentUser.UserAccountId.Value,
            null,
            MfaAuditEventCodes.PinSet);

        return Result<MfaStatusResponse>.Success(new MfaStatusResponse(
            credential.StatusCode,
            credential.LockedUntilUtc,
            credential.LastVerifiedAtUtc));
    }
}
