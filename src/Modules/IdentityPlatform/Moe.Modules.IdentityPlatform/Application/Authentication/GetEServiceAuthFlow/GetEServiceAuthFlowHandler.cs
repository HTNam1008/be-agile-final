using Microsoft.Extensions.Options;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared.Configuration;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetEServiceAuthFlow;

public sealed class GetEServiceAuthFlowHandler(IOptions<AuthenticationOptions> options)
    : IQueryHandler<GetEServiceAuthFlowQuery, EServiceAuthFlowResponse>
{
    public Task<Result<EServiceAuthFlowResponse>> Handle(GetEServiceAuthFlowQuery query, CancellationToken cancellationToken)
    {
        SingpassSchemeOptions singpass = options.Value.EServiceSingpass;

        EServiceAuthFlowResponse response = new(
            "Singpass",
            singpass.Mode,
            singpass.Authority.TrimEnd('/'),
            singpass.DiscoveryEndpoint,
            singpass.ClientId,
            singpass.Audience,
            singpass.Scopes,
            "/api/eservice/v1/auth/login",
            "/api/eservice/v1/auth/callback",
            "Start at /api/eservice/v1/auth/login. The backend completes Singpass/MockPass FAPI, resolves local eligibility, and sets a secure e-service session cookie.",
            true,
            false);

        return Task.FromResult(Result<EServiceAuthFlowResponse>.Success(response));
    }
}
