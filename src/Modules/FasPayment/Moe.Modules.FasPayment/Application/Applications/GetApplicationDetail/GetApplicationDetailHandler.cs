using System.Threading;
using System.Threading.Tasks;
using Moe.Application.Abstractions.Messaging;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;

internal sealed class GetApplicationDetailHandler(
    IFasApplicationRepository repository) : IQueryHandler<GetApplicationDetailQuery, GetApplicationDetailResponse>
{
    public async Task<Result<GetApplicationDetailResponse>> Handle(GetApplicationDetailQuery query, CancellationToken cancellationToken)
    {
        var response = await repository.GetApplicationDetailAsync(query.ApplicationId, cancellationToken);
        if (response == null)
        {
            return Result<GetApplicationDetailResponse>.Failure(new Error("Application.NotFound", "Application not found"));
        }

        return Result<GetApplicationDetailResponse>.Success(response);
    }
}
