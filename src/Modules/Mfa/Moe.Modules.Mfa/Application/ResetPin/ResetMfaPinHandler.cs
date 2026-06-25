using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.Mfa.Application.GetMfaStatus;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.ResetPin;

internal sealed class ResetMfaPinHandler(
    IMfaCredentialRepository credentials,
    IMfaAuditEventRepository auditEvents,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<ResetMfaPinCommand, MfaStatusResponse>
{
    public async Task<Result<MfaStatusResponse>> Handle(
        ResetMfaPinCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        if (command.LoginAccountId <= 0)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.InvalidLoginAccount);
        }

        string reason = command.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.ResetReasonRequired);
        }

        LoginMfaCredential? credential = await credentials.FindPinAsync(command.LoginAccountId, cancellationToken);
        if (credential is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.PinNotConfigured);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        credential.RequireReset(utcNow);

        auditEvents.Add(
            command.LoginAccountId,
            null,
            MfaAuditEventCodes.PinResetRequired,
            currentUser.UserAccountId.Value,
            reason);

        return Result<MfaStatusResponse>.Success(new MfaStatusResponse(
            credential.StatusCode,
            credential.LockedUntilUtc,
            credential.LastVerifiedAtUtc));
    }
}
