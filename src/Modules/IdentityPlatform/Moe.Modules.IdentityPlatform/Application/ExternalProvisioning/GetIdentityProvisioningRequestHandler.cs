using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning;

internal sealed class GetIdentityProvisioningRequestHandler(IIdentityProvisioningRequestRepository provisioningRequests)
    : IQueryHandler<GetIdentityProvisioningRequestQuery, IdentityProvisioningRequestResponse>
{
    public async Task<Result<IdentityProvisioningRequestResponse>> Handle(
        GetIdentityProvisioningRequestQuery query,
        CancellationToken cancellationToken)
    {
        IdentityProvisioningRequest? request = await provisioningRequests.FindByIdAsync(
            query.IdentityProvisioningRequestId,
            cancellationToken);

        if (request is null)
        {
            return Result<IdentityProvisioningRequestResponse>.Failure(IdentityErrors.ProvisioningRequestNotFound);
        }

        IdentityProvisioningRequestResponse response = new(
            request.Id,
            request.PersonId,
            request.IdentityProviderCode,
            request.ProvisioningStatusCode,
            request.ExternalSubjectId,
            request.CorrelationId,
            request.FailureCode,
            request.FailureReason);

        return Result<IdentityProvisioningRequestResponse>.Success(response);
    }
}
