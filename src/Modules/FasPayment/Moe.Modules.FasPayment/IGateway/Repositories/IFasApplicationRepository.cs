using System.Collections.Generic;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.Application.Applications.GetSchemeApplications;
using Moe.Modules.FasPayment.Application.Applications.GetApplicationDetail;

namespace Moe.Modules.FasPayment.IGateway.Repositories;

internal interface IFasApplicationRepository
{
    Task<FasApplication?> FindAsync(long applicationId, CancellationToken cancellationToken = default);
    Task AddAsync(FasApplication application, CancellationToken cancellationToken = default);
    Task AddDecisionAsync(FasApplicationReviewDecision decision, CancellationToken cancellationToken = default);
    Task<GetSchemeApplicationsResponse?> GetSchemeApplicationsAsync(long schemeId, CancellationToken cancellationToken = default);
    Task<GetApplicationDetailResponse?> GetApplicationDetailAsync(long applicationId, CancellationToken cancellationToken = default);
}
