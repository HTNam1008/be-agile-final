using System.Buffers.Binary;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moe.Application.Abstractions.Clock;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;

namespace Moe.Modules.IdentityPlatform.Infrastructure.Singpass;

internal sealed class MockPassFapiLoginGateway(
    HttpClient httpClient,
    IDataProtectionProvider dataProtectionProvider,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuthenticationOptions> options,
    IHostEnvironment environment,
    IClock clock,
    ILogger<MockPassFapiLoginGateway> logger) : ISingpassLoginGateway
{
    private const string ClientAssertionType = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    private const string LoginSessionProtectionPurpose = "Moe.StudentFinance.SingpassLoginSession.v1";
    private const int LoginSessionLifetimeMinutes = 5;
    private static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector loginSessionProtector = dataProtectionProvider.CreateProtector(LoginSessionProtectionPurpose);

    public async Task<SingpassLoginStartResult> StartLoginAsync(string? portalRedirectUri, CancellationToken cancellationToken)
    {
        SingpassSchemeOptions singpass = Options;
        string authority = Authority;
        string parEndpoint = $"{authority}/par";
        string authEndpoint = $"{authority}/auth";
        string redirectUri = ResolveRedirectUri(singpass);
        string state = RandomBase64Url(32);
        string nonce = RandomBase64Url(32);
        string codeVerifier = RandomBase64Url(48);
        string codeChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        using ECDsa dpopKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters dpopParameters = dpopKey.ExportParameters(true);
        string dpopX = Base64UrlEncoder.Encode(dpopParameters.Q.X!);
        string dpopY = Base64UrlEncoder.Encode(dpopParameters.Q.Y!);
        string dpopD = Base64UrlEncoder.Encode(dpopParameters.D!);
        string dpopJkt = CreateDpopJkt(dpopX, dpopY);
        string dpopToken = CreateDpopToken(parEndpoint, dpopKey, dpopX, dpopY);
        string clientAssertion = CreateClientAssertion(authority);

        using HttpRequestMessage request = new(HttpMethod.Post, parEndpoint);
        request.Headers.Add("DPoP", dpopToken);
        request.Content = new FormUrlEncodedContent(
        [
            new("client_id", singpass.ClientId),
            new("redirect_uri", redirectUri),
            new("response_type", "code"),
            new("scope", string.Join(' ', ResolveScopes(singpass))),
            new("state", state),
            new("nonce", nonce),
            new("acr_values", "urn:singpass:authentication:loa:1"),
            new("code_challenge", codeChallenge),
            new("code_challenge_method", "S256"),
            new("redirect_uri_https_type", "standard_https"),
            new("dpop_jkt", dpopJkt),
            new("client_assertion_type", ClientAssertionType),
            new("client_assertion", clientAssertion)
        ]);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        ParResponse? par = await response.Content.ReadFromJsonAsync<ParResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(par?.RequestUri))
        {
            throw new InvalidOperationException($"MockPass PAR failed with HTTP {(int)response.StatusCode}: {par?.Error ?? response.ReasonPhrase}");
        }

        SingpassLoginSession session = new(
            state,
            nonce,
            codeVerifier,
            redirectUri,
            dpopD,
            dpopX,
            dpopY,
            dpopJkt,
            clock.UtcNow.AddMinutes(LoginSessionLifetimeMinutes),
            portalRedirectUri);
        WriteLoginSessionCookie(session);

        string authorizationUrl = $"{authEndpoint}?client_id={Uri.EscapeDataString(singpass.ClientId)}&request_uri={Uri.EscapeDataString(par.RequestUri)}";
        return new SingpassLoginStartResult(authorizationUrl, state);
    }

    public async Task<SingpassLoginResult> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken)
    {
        SingpassLoginSession? session = ReadLoginSessionCookie();
        DeleteLoginSessionCookie();

        if (session is null)
        {
            logger.LogWarning("Singpass login callback did not contain a readable protected login session cookie.");
            throw new InvalidOperationException("The Singpass login session expired or the returned state is invalid.");
        }

        if (!string.Equals(session.State, state, StringComparison.Ordinal))
        {
            logger.LogWarning("Singpass login callback state mismatch. ExpectedState={ExpectedState} ReturnedState={ReturnedState}", session.State, state);
            throw new InvalidOperationException("The Singpass login session expired or the returned state is invalid.");
        }

        if (session.ExpiresAtUtc <= clock.UtcNow)
        {
            logger.LogWarning("Singpass login callback session expired. ExpiresAtUtc={ExpiresAtUtc} NowUtc={NowUtc}", session.ExpiresAtUtc, clock.UtcNow);
            throw new InvalidOperationException("The Singpass login session expired or the returned state is invalid.");
        }

        string tokenEndpoint = $"{Authority}/token";
        using ECDsa dpopKey = CreateDpopKey(session);
        string dpopToken = CreateDpopToken(tokenEndpoint, dpopKey, session.DpopPublicX, session.DpopPublicY);
        string clientAssertion = CreateClientAssertion(Authority);

        using HttpRequestMessage request = new(HttpMethod.Post, tokenEndpoint);
        request.Headers.Add("DPoP", dpopToken);
        request.Content = new FormUrlEncodedContent(
        [
            new("grant_type", "authorization_code"),
            new("code", code),
            new("redirect_uri", session.RedirectUri),
            new("code_verifier", session.CodeVerifier),
            new("client_assertion_type", ClientAssertionType),
            new("client_assertion", clientAssertion)
        ]);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(token?.IdToken))
        {
            throw new InvalidOperationException($"MockPass token exchange failed with HTTP {(int)response.StatusCode}: {token?.Error ?? response.ReasonPhrase}");
        }

        SingpassLoginResult login = await ValidateIdTokenAsync(token.IdToken, session.Nonce, cancellationToken);
        return login with { PortalRedirectUri = session.PortalRedirectUri };
    }

    private static ECDsa CreateDpopKey(SingpassLoginSession session)
    {
        return ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(session.DpopPublicX),
                Y = Base64UrlEncoder.DecodeBytes(session.DpopPublicY)
            },
            D = Base64UrlEncoder.DecodeBytes(session.DpopPrivateD)
        });
    }

    public string IssueLocalApiToken(SingpassLoginResult login)
    {
        SingpassSchemeOptions singpass = Options;
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        byte[] keyBytes = Encoding.UTF8.GetBytes(singpass.LocalTokenSigningKey);
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, login.ExternalSubjectId),
            new("name", login.DisplayName),
            new("amr", login.AuthenticationMethod)
        ];

        JwtSecurityToken token = new(
            issuer: Authority,
            audience: singpass.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.AddMinutes(singpass.LocalTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<SingpassLoginResult> ValidateIdTokenAsync(string idToken, string expectedNonce, CancellationToken cancellationToken)
    {
        SingpassSchemeOptions singpass = Options;
        JsonWebKeySet signingKeys = await LoadAspSigningKeysAsync(cancellationToken);
        string signedIdToken = DecryptIdToken(idToken);
        JsonWebTokenHandler handler = new();
        TokenValidationResult result = await handler.ValidateTokenAsync(signedIdToken, new TokenValidationParameters
        {
            ValidIssuer = Authority,
            ValidAudience = singpass.ClientId,
            IssuerSigningKeys = signingKeys.Keys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        });

        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            throw new InvalidOperationException($"MockPass id_token validation failed: {result.Exception?.Message ?? "Unknown validation error"}");
        }

        string subject = result.ClaimsIdentity.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? result.ClaimsIdentity.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("MockPass id_token did not contain sub.");
        string? nonce = result.ClaimsIdentity.FindFirst("nonce")?.Value;

        if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MockPass id_token nonce did not match the login session.");
        }

        string? subAttributesJson = result.ClaimsIdentity.FindFirst("sub_attributes")?.Value;
        using JsonDocument? subAttributes = TryParseJson(subAttributesJson);
        string identityNumber = result.ClaimsIdentity.FindFirst("sub_attributes.identity_number")?.Value
            ?? result.ClaimsIdentity.FindFirst("identity_number")?.Value
            ?? ReadJsonString(subAttributes, "identity_number")
            ?? "UNKNOWN";
        string displayName = result.ClaimsIdentity.FindFirst("sub_attributes.name")?.Value
            ?? result.ClaimsIdentity.FindFirst("name")?.Value
            ?? ReadJsonString(subAttributes, "name")
            ?? $"USER {identityNumber}";
        string acr = result.ClaimsIdentity.FindFirst("acr")?.Value ?? string.Empty;
        string amr = result.ClaimsIdentity.FindFirst("amr")?.Value ?? "pwd";

        return new SingpassLoginResult(Authority, subject, identityNumber, displayName, acr, amr);
    }

    private void WriteLoginSessionCookie(SingpassLoginSession session)
    {
        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("An active HTTP context is required to start a Singpass login.");
        string payload = JsonSerializer.Serialize(session, SessionJsonOptions);

        httpContext.Response.Cookies.Append(AuthenticationCookies.EServiceSingpassLoginSession, loginSessionProtector.Protect(payload), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(LoginSessionLifetimeMinutes)
        });
    }

    private SingpassLoginSession? ReadLoginSessionCookie()
    {
        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("An active HTTP context is required to complete a Singpass login.");

        if (!httpContext.Request.Cookies.TryGetValue(AuthenticationCookies.EServiceSingpassLoginSession, out string? protectedPayload)
            || string.IsNullOrWhiteSpace(protectedPayload))
        {
            return null;
        }

        try
        {
            string payload = loginSessionProtector.Unprotect(protectedPayload);
            return JsonSerializer.Deserialize<SingpassLoginSession>(payload, SessionJsonOptions);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            logger.LogWarning(ex, "Could not unprotect or deserialize the Singpass login session cookie.");
            return null;
        }
    }

    private void DeleteLoginSessionCookie()
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return;
        }

        httpContext.Response.Cookies.Delete(AuthenticationCookies.EServiceSingpassLoginSession, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps,
            Path = "/"
        });
    }

    private async Task<JsonWebKeySet> LoadAspSigningKeysAsync(CancellationToken cancellationToken)
    {
        string jwksJson = await httpClient.GetStringAsync($"{Authority}/.well-known/keys", cancellationToken);
        return new JsonWebKeySet(jwksJson);
    }

    private string CreateClientAssertion(string audience)
    {
        SingpassSchemeOptions singpass = Options;
        JsonWebKey signingJwk = LoadRpKey("sig");
        using ECDsa signingKey = CreateEcdsa(signingJwk, includePrivate: true);
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = singpass.ClientId,
            Subject = new ClaimsIdentity(
            [
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, singpass.ClientId),
                new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ]),
            Audience = audience,
            IssuedAt = clock.UtcNow.UtcDateTime,
            Expires = clock.UtcNow.UtcDateTime.AddMinutes(2),
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(signingKey)
                {
                    KeyId = signingJwk.Kid,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                },
                SecurityAlgorithms.EcdsaSha256),
            AdditionalHeaderClaims = new Dictionary<string, object> { ["typ"] = "JWT" }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private string CreateDpopToken(string endpoint, ECDsa key, string x, string y)
    {
        DateTime utcNow = clock.UtcNow.UtcDateTime;
        SecurityTokenDescriptor descriptor = new()
        {
            Claims = new Dictionary<string, object>
            {
                ["htu"] = endpoint,
                ["htm"] = "POST",
                ["jti"] = Guid.NewGuid().ToString(),
                ["nonce"] = Guid.NewGuid().ToString()
            },
            IssuedAt = utcNow,
            Expires = utcNow.AddMinutes(2),
            SigningCredentials = new SigningCredentials(
                new ECDsaSecurityKey(key)
                {
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                },
                SecurityAlgorithms.EcdsaSha256),
            AdditionalHeaderClaims = new Dictionary<string, object>
            {
                ["typ"] = "dpop+jwt",
                ["jwk"] = new Dictionary<string, string>
                {
                    ["kty"] = "EC",
                    ["crv"] = "P-256",
                    ["x"] = x,
                    ["y"] = y
                }
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private JsonWebKey LoadRpKey(string use)
    {
        string path = Options.MockPassRpPrivateJwksPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Authentication:EServiceSingpass:MockPassRpPrivateJwksPath is required for MockPass FAPI.");
        }

        string resolvedPath = ResolveKeyPath(path);
        string json = File.ReadAllText(resolvedPath);
        JsonWebKeySet keys = new(json);
        return keys.Keys.Single(x => string.Equals(x.Use, use, StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveKeyPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        string contentRootPath = Path.Combine(environment.ContentRootPath, configuredPath);

        if (File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        return Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    private static ECDsa CreateEcdsa(JsonWebKey jwk, bool includePrivate)
    {
        ECParameters parameters = new()
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(jwk.X),
                Y = Base64UrlEncoder.DecodeBytes(jwk.Y)
            }
        };

        if (includePrivate)
        {
            parameters.D = Base64UrlEncoder.DecodeBytes(jwk.D);
        }

        return ECDsa.Create(parameters);
    }

    private string DecryptIdToken(string encryptedIdToken)
    {
        string[] parts = encryptedIdToken.Split('.');

        if (parts.Length != 5)
        {
            throw new InvalidOperationException("MockPass id_token was not a compact JWE.");
        }

        JweHeader header = JsonSerializer.Deserialize<JweHeader>(
            Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[0]))) ?? throw new InvalidOperationException("MockPass id_token JWE header was invalid.");

        if (!string.Equals(header.Alg, "ECDH-ES+A256KW", StringComparison.Ordinal)
            || !string.Equals(header.Enc, "A256GCM", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"MockPass id_token JWE alg/enc is not supported: {header.Alg}/{header.Enc}.");
        }

        JsonWebKey rpEncryptionKey = LoadRpKey("enc");
        byte[] sharedSecret = DeriveEcdhSharedSecret(rpEncryptionKey, header.Epk);
        byte[] keyEncryptionKey = ConcatKdf(sharedSecret, header.Alg, 256, header.Apu, header.Apv);
        byte[] contentEncryptionKey = AesKeyUnwrap(keyEncryptionKey, Base64UrlEncoder.DecodeBytes(parts[1]));
        byte[] iv = Base64UrlEncoder.DecodeBytes(parts[2]);
        byte[] cipherText = Base64UrlEncoder.DecodeBytes(parts[3]);
        byte[] tag = Base64UrlEncoder.DecodeBytes(parts[4]);
        byte[] plainText = new byte[cipherText.Length];

        using AesGcm aes = new(contentEncryptionKey, tag.Length);
        aes.Decrypt(iv, cipherText, tag, plainText, Encoding.ASCII.GetBytes(parts[0]));

        return Encoding.UTF8.GetString(plainText);
    }

    private static byte[] DeriveEcdhSharedSecret(JsonWebKey privateJwk, JweEphemeralPublicKey? publicJwk)
    {
        if (publicJwk is null)
        {
            throw new InvalidOperationException("MockPass id_token JWE header did not contain an ephemeral public key.");
        }

        using ECDiffieHellman privateKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(privateJwk.X),
                Y = Base64UrlEncoder.DecodeBytes(privateJwk.Y)
            },
            D = Base64UrlEncoder.DecodeBytes(privateJwk.D)
        });
        using ECDiffieHellman publicKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64UrlEncoder.DecodeBytes(publicJwk.X),
                Y = Base64UrlEncoder.DecodeBytes(publicJwk.Y)
            }
        });

        return privateKey.DeriveRawSecretAgreement(publicKey.PublicKey);
    }

    private static byte[] ConcatKdf(byte[] sharedSecret, string algorithm, int keyDataLengthBits, string? apu, string? apv)
    {
        byte[] algorithmId = LengthPrefixed(Encoding.ASCII.GetBytes(algorithm));
        byte[] partyUInfo = LengthPrefixed(string.IsNullOrWhiteSpace(apu) ? [] : Base64UrlEncoder.DecodeBytes(apu));
        byte[] partyVInfo = LengthPrefixed(string.IsNullOrWhiteSpace(apv) ? [] : Base64UrlEncoder.DecodeBytes(apv));
        byte[] suppPubInfo = Int32ToBigEndian(keyDataLengthBits);
        byte[] otherInfo = [.. algorithmId, .. partyUInfo, .. partyVInfo, .. suppPubInfo];
        byte[] counter = Int32ToBigEndian(1);
        byte[] hash = SHA256.HashData([.. counter, .. sharedSecret, .. otherInfo]);
        return hash[..(keyDataLengthBits / 8)];
    }

    private static byte[] AesKeyUnwrap(byte[] keyEncryptionKey, byte[] wrappedKey)
    {
        if (wrappedKey.Length < 24 || wrappedKey.Length % 8 != 0)
        {
            throw new InvalidOperationException("MockPass id_token encrypted key has an invalid AES key wrap length.");
        }

        const ulong defaultInitialValue = 0xA6A6A6A6A6A6A6A6;
        int n = (wrappedKey.Length / 8) - 1;
        byte[] a = wrappedKey[..8];
        byte[][] r = Enumerable.Range(0, n)
            .Select(i => wrappedKey[((i + 1) * 8)..((i + 2) * 8)])
            .ToArray();

        using Aes aes = Aes.Create();
        aes.Key = keyEncryptionKey;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using ICryptoTransform decryptor = aes.CreateDecryptor();

        for (int j = 5; j >= 0; j--)
        {
            for (int i = n; i >= 1; i--)
            {
                ulong t = (ulong)((n * j) + i);
                byte[] block = [.. XorWithUInt64(a, t), .. r[i - 1]];
                byte[] decrypted = decryptor.TransformFinalBlock(block, 0, block.Length);
                a = decrypted[..8];
                r[i - 1] = decrypted[8..16];
            }
        }

        if (BinaryPrimitives.ReadUInt64BigEndian(a) != defaultInitialValue)
        {
            throw new InvalidOperationException("MockPass id_token encrypted key failed AES key unwrap integrity check.");
        }

        return r.SelectMany(x => x).ToArray();
    }

    private static byte[] XorWithUInt64(byte[] value, ulong operand)
    {
        ulong left = BinaryPrimitives.ReadUInt64BigEndian(value);
        byte[] output = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(output, left ^ operand);
        return output;
    }

    private static byte[] LengthPrefixed(byte[] value)
        => [.. Int32ToBigEndian(value.Length), .. value];

    private static byte[] Int32ToBigEndian(int value)
    {
        byte[] output = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(output, value);
        return output;
    }

    private static string CreateDpopJkt(string x, string y)
    {
        string thumbprintJson = $"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{x}\",\"y\":\"{y}\"}}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(thumbprintJson)));
    }

    private static JsonDocument? TryParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadJsonString(JsonDocument? document, string propertyName)
    {
        if (document is null
            || !document.RootElement.TryGetProperty(propertyName, out JsonElement property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string RandomBase64Url(int byteCount)
        => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(byteCount));

    private static IReadOnlyCollection<string> ResolveScopes(SingpassSchemeOptions options)
        => options.Scopes.Length == 0 ? ["openid", "user.identity", "uinfin"] : options.Scopes;

    private static string ResolveRedirectUri(SingpassSchemeOptions options)
        => string.IsNullOrWhiteSpace(options.RedirectUri)
            ? "http://localhost:7001/api/eservice/v1/auth/callback"
            : options.RedirectUri;

    private SingpassSchemeOptions Options => options.Value.EServiceSingpass;
    private string Authority => Options.Authority.TrimEnd('/');

    private sealed record ParResponse(
        [property: JsonPropertyName("request_uri")] string? RequestUri,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("error")] string? Error);

    private sealed record JweHeader(
        [property: JsonPropertyName("alg")] string Alg,
        [property: JsonPropertyName("enc")] string Enc,
        [property: JsonPropertyName("epk")] JweEphemeralPublicKey? Epk,
        [property: JsonPropertyName("apu")] string? Apu,
        [property: JsonPropertyName("apv")] string? Apv);

    private sealed record JweEphemeralPublicKey(
        [property: JsonPropertyName("kty")] string Kty,
        [property: JsonPropertyName("crv")] string Crv,
        [property: JsonPropertyName("x")] string X,
        [property: JsonPropertyName("y")] string Y);
}
