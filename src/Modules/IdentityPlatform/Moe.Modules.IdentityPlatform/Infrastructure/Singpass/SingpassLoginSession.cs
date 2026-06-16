namespace Moe.Modules.IdentityPlatform.Infrastructure.Singpass;

internal sealed record SingpassLoginSession(
    string State,
    string Nonce,
    string CodeVerifier,
    string RedirectUri,
    string DpopPrivateKey,
    string DpopPublicX,
    string DpopPublicY,
    string DpopJkt);
