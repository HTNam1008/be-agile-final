using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling.Infrastructure.Persistence;

namespace Moe.Modules.CourseBilling;

public sealed class CourseBillingModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BillConfiguration());
        modelBuilder.ApplyConfiguration(new BillLineConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new CourseEnrollmentConfiguration());
        modelBuilder.ApplyConfiguration(new CourseFeeConfiguration());
        modelBuilder.ApplyConfiguration(new CourseTargetConfiguration());
        modelBuilder.ApplyConfiguration(new FeeComponentConfiguration());
    }
}
