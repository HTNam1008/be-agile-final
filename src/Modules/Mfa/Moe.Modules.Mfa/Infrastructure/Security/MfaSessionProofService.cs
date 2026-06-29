using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.Mfa.Application;

namespace Moe.Modules.Mfa.Infrastructure.Security;

internal sealed class MfaSessionProofService(
    IHttpContextAccessor httpContextAccessor,
    IDataProtectionProvider dataProtectionProvider,
    ICurrentUser currentUser,
    IClock clock) : IMfaSessionProofService
{
    private const string AdminProofCookie = "moe_admin_mfa_session";
    private const string EServiceProofCookie = "moe_portal_mfa_session";
    private static readonly TimeSpan ProofLifetime = TimeSpan.FromMinutes(60);
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Moe.Mfa.SessionProof.v1");

    public bool IsCurrentSessionVerified()
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null || currentUser.UserAccountId is null || !TryResolveDomain(out MfaAuthDomain domain))
        {
            return false;
        }

        if (!context.Request.Cookies.TryGetValue(domain.ProofCookieName, out string? protectedProof)
            || !context.Request.Cookies.TryGetValue(domain.AuthCookieName, out string? authToken)
            || string.IsNullOrWhiteSpace(protectedProof)
            || string.IsNullOrWhiteSpace(authToken))
        {
            return false;
        }

        try
        {
            MfaSessionProof? proof = JsonSerializer.Deserialize<MfaSessionProof>(_protector.Unprotect(protectedProof));
            if (proof is null
                || proof.UserAccountId != currentUser.UserAccountId.Value
                || !string.Equals(proof.PortalCode, currentUser.Portal, StringComparison.Ordinal)
                || proof.ExpiresAtUtc <= clock.UtcNow)
            {
                return false;
            }

            return FixedTimeEquals(proof.AuthSessionFingerprint, Fingerprint(authToken));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    public void MarkCurrentSessionVerified()
    {
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is null || currentUser.UserAccountId is null || !TryResolveDomain(out MfaAuthDomain domain))
        {
            return;
        }

        if (!context.Request.Cookies.TryGetValue(domain.AuthCookieName, out string? authToken)
            || string.IsNullOrWhiteSpace(authToken))
        {
            return;
        }

        DateTimeOffset expiresAtUtc = clock.UtcNow.Add(ProofLifetime);
        var proof = new MfaSessionProof(
            currentUser.UserAccountId.Value,
            currentUser.Portal,
            Fingerprint(authToken),
            expiresAtUtc);

        context.Response.Cookies.Append(
            domain.ProofCookieName,
            _protector.Protect(JsonSerializer.Serialize(proof)),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = context.Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                MaxAge = ProofLifetime
            });
    }

    private bool TryResolveDomain(out MfaAuthDomain domain)
    {
        if (string.Equals(currentUser.Portal, PortalCodes.Admin, StringComparison.Ordinal))
        {
            domain = new MfaAuthDomain(AuthenticationCookies.AdminSession, AdminProofCookie);
            return true;
        }

        if (string.Equals(currentUser.Portal, PortalCodes.EService, StringComparison.Ordinal))
        {
            domain = new MfaAuthDomain(AuthenticationCookies.EServiceSession, EServiceProofCookie);
            return true;
        }

        domain = default;
        return false;
    }

    private static string Fingerprint(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private readonly record struct MfaAuthDomain(string AuthCookieName, string ProofCookieName);
    private sealed record MfaSessionProof(
        long UserAccountId,
        string PortalCode,
        string AuthSessionFingerprint,
        DateTimeOffset ExpiresAtUtc);
}
