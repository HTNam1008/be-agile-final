using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.GetMfaStatus;

internal sealed class GetMfaStatusHandler(
    IMfaCredentialRepository credentials,
    IMfaSessionProofService sessionProof,
    ICurrentUser currentUser,
    IClock clock) : IQueryHandler<GetMfaStatusQuery, MfaStatusResponse>
{
    public async Task<Result<MfaStatusResponse>> Handle(GetMfaStatusQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<MfaStatusResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        LoginMfaCredential? credential = await credentials.FindPinAsync(
            currentUser.UserAccountId.Value,
            cancellationToken);

        if (credential is null)
        {
            return Result<MfaStatusResponse>.Success(new MfaStatusResponse("NOT_CONFIGURED", null, null, false));
        }

        string statusCode = credential.IsLocked(clock.UtcNow.UtcDateTime)
            ? MfaCredentialStatusCodes.Locked
            : credential.StatusCode;

        return Result<MfaStatusResponse>.Success(new MfaStatusResponse(
            statusCode,
            credential.LockedUntilUtc,
            credential.LastVerifiedAtUtc,
            sessionProof.IsCurrentSessionVerified()));
    }
}
