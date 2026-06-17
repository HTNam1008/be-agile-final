using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Repositories;

internal sealed class TopUpCampaignRepository(MoeDbContext dbContext) : ITopUpCampaignRepository
{
    public Task<TopUpCampaign?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<TopUpCampaign>().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
