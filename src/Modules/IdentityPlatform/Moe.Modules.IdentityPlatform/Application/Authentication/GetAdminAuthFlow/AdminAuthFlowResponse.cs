namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;

public sealed record AdminAuthFlowResponse(
    string Provider,
    string Authority,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string ClientId,
    string Audience,
    IReadOnlyCollection<string> Scopes,
    string TokenUsage,
    bool BackendIssuesToken,
    bool SelfRegistrationAllowed);
