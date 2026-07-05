using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.RecoverPin;

internal sealed class ValidateMfaPinRecoveryHandler(
    IMfaChallengeRepository challenges,
    IClock clock) : IQueryHandler<ValidateMfaPinRecoveryQuery, MfaPinRecoveryTokenStatus>
{
    public async Task<Result<MfaPinRecoveryTokenStatus>> Handle(
        ValidateMfaPinRecoveryQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.Token, out Guid challengeId))
            return Success(false, "INVALID");

        LoginMfaChallenge? challenge = await challenges.FindByIdAsync(challengeId, cancellationToken);
        if (challenge is null || challenge.PurposeCode != MfaChallengePurposeCodes.Recovery)
            return Success(false, "INVALID");

        if (challenge.StatusCode == MfaChallengeStatusCodes.Verified)
            return Success(false, "USED");

        if (challenge.IsExpired(clock.UtcNow.UtcDateTime) || challenge.StatusCode == MfaChallengeStatusCodes.Expired)
            return Success(false, "EXPIRED");

        if (challenge.StatusCode != MfaChallengeStatusCodes.Pending)
            return Success(false, "INVALID");

        return Success(true, "VALID");
    }

    private static Result<MfaPinRecoveryTokenStatus> Success(bool isValid, string status) =>
        Result<MfaPinRecoveryTokenStatus>.Success(new MfaPinRecoveryTokenStatus(isValid, status));
}
