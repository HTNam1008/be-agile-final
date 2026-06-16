using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Infrastructure.Persistence;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CourseFasSchemeConfiguration());
        modelBuilder.ApplyConfiguration(new FasApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new FasRuleConfiguration());
        modelBuilder.ApplyConfiguration(new FasSchemeConfiguration());
        modelBuilder.ApplyConfiguration(new FasSubsidyConfiguration());
        modelBuilder.ApplyConfiguration(new FasTierBenefitConfiguration());
        modelBuilder.ApplyConfiguration(new FasTierConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentPartConfiguration());
    }
}
