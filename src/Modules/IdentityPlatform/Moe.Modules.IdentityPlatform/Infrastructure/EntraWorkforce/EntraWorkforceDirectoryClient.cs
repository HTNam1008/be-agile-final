using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Moe.Modules.IdentityPlatform.IGateway.Admin;

namespace Moe.Modules.IdentityPlatform.Infrastructure.EntraWorkforce;

internal sealed class EntraWorkforceDirectoryClient(
    HttpClient httpClient,
    IOptions<EntraWorkforceDirectoryOptions> options) : IEntraWorkforceDirectoryClient
{
    public async Task<CreateEntraUserGatewayResult> CreateUserAsync(
        CreateEntraUserGatewayRequest request,
        CancellationToken cancellationToken)
    {
        string accessToken = await GetAccessTokenAsync(cancellationToken);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, $"{options.Value.GraphBaseUrl.TrimEnd('/')}/users");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = JsonContent.Create(new CreateGraphUserRequest(
            request.AccountEnabled,
            request.DisplayName,
            request.MailNickname,
            request.UserPrincipalName,
            new PasswordProfile(request.TemporaryPassword, true)));

        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        GraphUserResponse? graphUser = await response.Content.ReadFromJsonAsync<GraphUserResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode || graphUser is null || string.IsNullOrWhiteSpace(graphUser.Id))
        {
            string graphCode = string.IsNullOrWhiteSpace(graphUser?.Error?.Code)
                ? response.StatusCode.ToString()
                : graphUser.Error.Code;
            string errorText = graphUser?.Error?.Message ?? response.ReasonPhrase ?? "Microsoft Graph user creation failed.";
            throw new InvalidOperationException($"Microsoft Graph returned {(int)response.StatusCode} ({graphCode}): {errorText}");
        }

        return new CreateEntraUserGatewayResult(
            graphUser.Id,
            graphUser.UserPrincipalName ?? request.UserPrincipalName,
            graphUser.DisplayName ?? request.DisplayName,
            graphUser.Mail);
    }

    public async Task DeleteUserAsync(
        string externalObjectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalObjectId))
        {
            return;
        }

        string accessToken = await GetAccessTokenAsync(cancellationToken);
        string escapedId = Uri.EscapeDataString(externalObjectId);

        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, $"{options.Value.GraphBaseUrl.TrimEnd('/')}/users/{escapedId}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Microsoft Graph user cleanup failed: {response.ReasonPhrase}");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        EntraWorkforceDirectoryOptions directoryOptions = options.Value;
        using HttpRequestMessage request = new(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{directoryOptions.TenantId}/oauth2/v2.0/token");
        request.Content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", directoryOptions.ClientId),
            new KeyValuePair<string, string>("client_secret", directoryOptions.ClientSecret),
            new KeyValuePair<string, string>("scope", "https://graph.microsoft.com/.default"),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        ]);

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(token?.AccessToken))
        {
            throw new InvalidOperationException($"Microsoft Graph access token request failed with HTTP {(int)response.StatusCode}.");
        }

        return token.AccessToken;
    }

    private sealed record CreateGraphUserRequest(
        [property: JsonPropertyName("accountEnabled")] bool AccountEnabled,
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("mailNickname")] string MailNickname,
        [property: JsonPropertyName("userPrincipalName")] string UserPrincipalName,
        [property: JsonPropertyName("passwordProfile")] PasswordProfile PasswordProfile);

    private sealed record PasswordProfile(
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("forceChangePasswordNextSignIn")] bool ForceChangePasswordNextSignIn);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record GraphUserResponse(
        string? Id,
        string? UserPrincipalName,
        string? DisplayName,
        string? Mail,
        GraphErrorContainer? Error);

    private sealed record GraphErrorContainer(
        string? Code,
        string? Message);
}
