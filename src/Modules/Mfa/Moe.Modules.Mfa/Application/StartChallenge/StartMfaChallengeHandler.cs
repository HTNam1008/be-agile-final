using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.StartChallenge;

internal sealed class StartMfaChallengeHandler(
    IMfaCredentialRepository credentials,
    IMfaChallengeRepository challenges,
    IMfaAuditEventRepository auditEvents,
    ICurrentUser currentUser,
    IClock clock,
    IOptions<MfaOptions> options) : ICommandHandler<StartMfaChallengeCommand, MfaChallengeResponse>
{
    public async Task<Result<MfaChallengeResponse>> Handle(
        StartMfaChallengeCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null)
        {
            return Result<MfaChallengeResponse>.Failure(MfaErrors.AuthenticatedUserRequired);
        }

        DateTime utcNow = clock.UtcNow.UtcDateTime;
        LoginMfaCredential? credential = await credentials.FindActivePinAsync(
            currentUser.UserAccountId.Value,
            cancellationToken);

        if (credential is null)
        {
            return Result<MfaChallengeResponse>.Failure(MfaErrors.PinNotConfigured);
        }

        if (credential.IsLocked(utcNow))
        {
            return Result<MfaChallengeResponse>.Failure(MfaErrors.CredentialLocked);
        }

        LoginMfaChallenge challenge = LoginMfaChallenge.Create(
            currentUser.UserAccountId.Value,
            MfaChallengePurposeCodes.Login,
            TimeSpan.FromMinutes(options.Value.ChallengeLifetimeMinutes),
            utcNow);

        challenges.Add(challenge);
        await challenges.SaveChangesAsync(cancellationToken);

        auditEvents.Add(
            currentUser.UserAccountId.Value,
            challenge.Id,
            MfaAuditEventCodes.ChallengeStarted);

        return Result<MfaChallengeResponse>.Success(new MfaChallengeResponse(
            challenge.Id,
            challenge.ExpiresAtUtc));
    }
}
