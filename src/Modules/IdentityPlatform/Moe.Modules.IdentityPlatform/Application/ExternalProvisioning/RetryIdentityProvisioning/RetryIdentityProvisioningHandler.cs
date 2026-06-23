using Moe.Application.Abstractions.Messaging;
using Moe.Modules.IdentityPlatform.Application;
using Moe.Modules.IdentityPlatform.Domain.Iam;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.IdentityPlatform.Application.ExternalProvisioning.RetryIdentityProvisioning;

internal sealed class RetryIdentityProvisioningHandler(IIdentityProvisioningRequestRepository provisioningRequests)
    : ICommandHandler<RetryIdentityProvisioningCommand, IdentityProvisioningRequestResponse>
{
    public async Task<Result<IdentityProvisioningRequestResponse>> Handle(
        RetryIdentityProvisioningCommand command,
        CancellationToken cancellationToken)
    {
        IdentityProvisioningRequest? request = await provisioningRequests.FindByIdAsync(
            command.IdentityProvisioningRequestId,
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
