using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Infrastructure.Persistence;

namespace Moe.Modules.EducationAccountTopUp;

public sealed class EducationAccountTopUpModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountHoldConfiguration());
        modelBuilder.ApplyConfiguration(new AccountSettlementConfiguration());
        modelBuilder.ApplyConfiguration(new AccountTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new EducationAccountConfiguration());
        modelBuilder.ApplyConfiguration(new EducationAccountLifecycleRunConfiguration());
        modelBuilder.ApplyConfiguration(new EducationAccountLifecycleRunItemConfiguration());
        modelBuilder.ApplyConfiguration(new SettlementPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new TopUpCampaignConfiguration());
        modelBuilder.ApplyConfiguration(new TopUpCampaignRecipientConfiguration());
        modelBuilder.ApplyConfiguration(new TopUpCampaignRuleConfiguration());
        modelBuilder.ApplyConfiguration(new TopUpRunConfiguration());
        modelBuilder.ApplyConfiguration(new TopUpTransactionConfiguration());
    }
}
