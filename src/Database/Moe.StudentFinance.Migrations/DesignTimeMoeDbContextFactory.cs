using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.CourseBilling;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.FasPayment;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.Mfa;
using Moe.Modules.AiCopilot.Infrastructure.Persistence;
using Moe.Modules.Notifications;
using Moe.StudentFinance.Persistence;

namespace Moe.StudentFinance.Migrations;

public sealed class DesignTimeMoeDbContextFactory : IDesignTimeDbContextFactory<MoeDbContext>
{
    public MoeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MOE_DESIGN_CONNECTION")
            ?? "Server=localhost\\SQLEXPRESS;Database=SF;Integrated Security=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlServer(connection, x => x.MigrationsAssembly(typeof(DesignTimeMoeDbContextFactory).Assembly.FullName))
            .Options;
        IModelConfigurationContributor[] contributors =
        [
            new IdentityPlatformModelConfiguration(),
            new EducationAccountTopUpModelConfiguration(),
            new CourseBillingModelConfiguration(),
            new FasPaymentModelConfiguration(),
            new MfaModelConfiguration(),
            new NotificationModelConfiguration(),
            new AiModelConfiguration()
        ];
        return new MoeDbContext(options, contributors);
    }
}
