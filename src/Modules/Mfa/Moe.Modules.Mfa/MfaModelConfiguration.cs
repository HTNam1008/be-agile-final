using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.Mfa.Infrastructure.Persistence;

namespace Moe.Modules.Mfa;

public sealed class MfaModelConfiguration : IModelConfigurationContributor
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LoginMfaAuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new LoginMfaChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new LoginMfaCredentialConfiguration());
    }
}
