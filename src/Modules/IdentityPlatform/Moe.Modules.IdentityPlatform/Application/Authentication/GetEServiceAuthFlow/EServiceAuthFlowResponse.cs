namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetEServiceAuthFlow;

public sealed record EServiceAuthFlowResponse(
    string Provider,
    string Mode,
    string Authority,
    string DiscoveryEndpoint,
    string ClientId,
    string Audience,
    IReadOnlyCollection<string> Scopes,
    string LoginEndpoint,
    string CallbackEndpoint,
    string TokenUsage,
    bool BackendIssuesToken,
    bool UsesMicrosoftEntraId);
