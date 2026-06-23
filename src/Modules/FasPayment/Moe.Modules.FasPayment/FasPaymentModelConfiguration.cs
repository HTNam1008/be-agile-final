using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.FasPayment.Infrastructure.Persistence;

namespace Moe.Modules.FasPayment;

public sealed class FasPaymentModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FasApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new FasApplicationReviewDecisionConfiguration());
        modelBuilder.ApplyConfiguration(new FasApplicationSchemeConfiguration());
        modelBuilder.ApplyConfiguration(new FasDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new FasDeclarationConfiguration());
        modelBuilder.ApplyConfiguration(new FasStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new FasActiveSchemeConfiguration());
        modelBuilder.ApplyConfiguration(new FasSchemeConfiguration());
        modelBuilder.ApplyConfiguration(new FasSchemeCourseConfiguration());
        modelBuilder.ApplyConfiguration(new FasTierConfiguration());
        modelBuilder.ApplyConfiguration(new FasTierCriteriaConfiguration());
        modelBuilder.ApplyConfiguration(new FasTierCriteriaNationalityConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentPartConfiguration());
    }
}
