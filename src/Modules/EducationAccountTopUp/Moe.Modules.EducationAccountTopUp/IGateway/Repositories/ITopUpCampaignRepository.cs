using Moe.Modules.EducationAccountTopUp.Domain.TopUps;

namespace Moe.Modules.EducationAccountTopUp.IGateway.Repositories;

public interface ITopUpCampaignRepository
{
    Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
}
