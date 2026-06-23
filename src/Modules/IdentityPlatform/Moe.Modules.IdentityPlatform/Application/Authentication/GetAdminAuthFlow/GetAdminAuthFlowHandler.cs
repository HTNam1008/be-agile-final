using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Configuration;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetAdminAuthFlow;

public sealed class GetAdminAuthFlowHandler(IOptions<AuthenticationOptions> options)
    : IQueryHandler<GetAdminAuthFlowQuery, AdminAuthFlowResponse>
{
    public Task<Result<AdminAuthFlowResponse>> Handle(GetAdminAuthFlowQuery query, CancellationToken cancellationToken)
    {
        JwtSchemeOptions admin = options.Value.AdminEntra;
        string authority = admin.Authority.TrimEnd('/');
        string oauthBaseUrl = authority.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase)
            ? authority[..^"/v2.0".Length]
            : authority;

        AdminAuthFlowResponse response = new(
            "Microsoft Entra ID",
            authority,
            $"{oauthBaseUrl}/oauth2/v2.0/authorize",
            $"{oauthBaseUrl}/oauth2/v2.0/token",
            admin.ClientId,
            admin.Audience,
            admin.Scopes.Length == 0 ? [BuildDefaultScope(admin.Audience)] : admin.Scopes,
            "Exchange the authorization code for an access_token, then POST it once to /api/admin/v1/auth/session. Authenticated admin API calls use the secure backend session cookie.",
            false,
            false);

        return Task.FromResult(Result<AdminAuthFlowResponse>.Success(response));
    }

    private static string BuildDefaultScope(string audience)
        => $"{audience.TrimEnd('/')}/.default";
}
