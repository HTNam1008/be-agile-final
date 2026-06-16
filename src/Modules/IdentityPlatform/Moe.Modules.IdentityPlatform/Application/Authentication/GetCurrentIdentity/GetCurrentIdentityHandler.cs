using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.IGateway.Authentication;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.Authentication.GetCurrentIdentity;

public sealed class GetCurrentIdentityHandler(ILocalIdentityDirectory localIdentityDirectory)
    : IQueryHandler<GetCurrentIdentityQuery, LocalIdentitySummary>
{
    public async Task<Result<LocalIdentitySummary>> Handle(GetCurrentIdentityQuery query, CancellationToken cancellationToken)
    {
        LocalIdentitySummary? identity = await localIdentityDirectory.GetCurrentAsync(cancellationToken);

        if (identity is null)
        {
            return Result<LocalIdentitySummary>.Failure(IdentityErrors.AuthenticatedUserRequired);
        }

        return Result<LocalIdentitySummary>.Success(identity);
    }
}
