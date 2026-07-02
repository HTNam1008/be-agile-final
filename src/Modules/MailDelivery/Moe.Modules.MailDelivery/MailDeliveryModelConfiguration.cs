using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.MailDelivery.Infrastructure.Persistence;

namespace Moe.Modules.MailDelivery;

public sealed class MailDeliveryModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EmailNotificationConfiguration());
    }
}
