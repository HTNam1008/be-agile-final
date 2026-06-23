namespace Moe.Modules.IdentityPlatform.IGateway.Authentication;

public sealed record SingpassLoginStartResult(string AuthorizationUrl, string State);

public sealed record SingpassLoginResult(
    string ExternalIssuer,
    string ExternalSubjectId,
    string IdentityNumber,
    string DisplayName,
    string AuthenticationContext,
    string AuthenticationMethod,
    string? PortalRedirectUri = null);

public interface ISingpassLoginGateway
{
    Task<SingpassLoginStartResult> StartLoginAsync(
        string? portalRedirectUri,
        CancellationToken cancellationToken);

    Task<SingpassLoginResult> CompleteLoginAsync(
        string authorizationCode,
        string state,
        CancellationToken cancellationToken);

    string IssueLocalApiToken(SingpassLoginResult login);
}
