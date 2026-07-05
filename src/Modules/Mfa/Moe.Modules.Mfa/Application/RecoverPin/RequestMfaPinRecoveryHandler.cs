using System.Net;
using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Modules.MailDelivery.IGateway;
using Moe.Modules.Mfa.Domain;
using Moe.Modules.Mfa.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.Mfa.Application.RecoverPin;

internal sealed class RequestMfaPinRecoveryHandler(
    IMfaCredentialRepository credentials, IMfaChallengeRepository challenges,
    IMfaAuditEventRepository auditEvents, IMfaRecoveryContactResolver contacts,
    IEmailDeliveryGateway emailDelivery, ICurrentUser currentUser, IClock clock,
    IOptions<MfaOptions> options) : ICommandHandler<RequestMfaPinRecoveryCommand, bool>
{
    public async Task<Result<bool>> Handle(RequestMfaPinRecoveryCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserAccountId is null) return Result<bool>.Failure(MfaErrors.AuthenticatedUserRequired);
        long accountId = currentUser.UserAccountId.Value;
        if (await credentials.FindPinAsync(accountId, cancellationToken) is null) return Result<bool>.Failure(MfaErrors.PinNotConfigured);
        string? email = await contacts.ResolveEmailAsync(accountId, cancellationToken);
        if (string.IsNullOrWhiteSpace(email)) return Result<bool>.Failure(MfaErrors.RecoveryEmailUnavailable);

        DateTime now = clock.UtcNow.UtcDateTime;
        LoginMfaChallenge challenge = LoginMfaChallenge.Create(accountId, MfaChallengePurposeCodes.Recovery,
            TimeSpan.FromMinutes(options.Value.RecoveryLinkLifetimeMinutes), now);
        string url = $"{options.Value.RecoveryPageUrl.TrimEnd('/')}?token={challenge.Id:N}";
        string text = $"A request was made to recover your MFA PIN. Use this link within {options.Value.RecoveryLinkLifetimeMinutes} minutes: {url}\n\nIf you did not request this, ignore this email.";
        string html = $"<p>A request was made to recover your MFA PIN.</p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Reset MFA PIN</a></p><p>This link expires in {options.Value.RecoveryLinkLifetimeMinutes} minutes. If you did not request this, ignore this email.</p>";

        // Persist the principal row first. LoginMfaAuditEvent has a database FK to
        // LoginMfaChallenge, but the EF model has no navigation from which to infer
        // insert ordering.
        challenges.Add(challenge);
        await challenges.SaveChangesAsync(cancellationToken);

        Result sent = await emailDelivery.SendAsync(new EmailDeliveryMessage(email, "Recover your MFA PIN", text, html), cancellationToken);
        if (sent.IsFailure)
        {
            challenge.MarkFailed();
            await challenges.SaveChangesAsync(cancellationToken);
            return Result<bool>.Failure(sent.Error);
        }

        auditEvents.Add(accountId, challenge.Id, MfaAuditEventCodes.RecoveryRequested);
        await challenges.SaveChangesAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
