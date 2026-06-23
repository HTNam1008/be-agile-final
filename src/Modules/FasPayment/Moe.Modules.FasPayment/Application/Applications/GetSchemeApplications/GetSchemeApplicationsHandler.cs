using System.Threading;
using System.Threading.Tasks;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;

internal sealed class GetSchemeApplicationsHandler(
    IFasApplicationRepository repository) : IQueryHandler<GetSchemeApplicationsQuery, GetSchemeApplicationsResponse>
{
    public async Task<Result<GetSchemeApplicationsResponse>> Handle(GetSchemeApplicationsQuery query, CancellationToken cancellationToken)
    {
        var response = await repository.GetSchemeApplicationsAsync(query.SchemeId, cancellationToken);
        if (response == null)
        {
            return Result<GetSchemeApplicationsResponse>.Failure(new Error("Scheme.NotFound", "Scheme not found"));
        }

        return Result<GetSchemeApplicationsResponse>.Success(response);
    }
}
