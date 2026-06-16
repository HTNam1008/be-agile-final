using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.CourseBilling;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.FasPayment;
using Moe.StudentFinance.Persistence;

namespace Moe.StudentFinance.Migrations;

public sealed class DesignTimeMoeDbContextFactory : IDesignTimeDbContextFactory<MoeDbContext>
{
    public MoeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("MOE_DESIGN_CONNECTION")
            ?? "Server=localhost,1433;Database=MOEStudentFinance;User Id=sa;Password=Change_me_123!;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseSqlServer(connection, x => x.MigrationsAssembly(typeof(DesignTimeMoeDbContextFactory).Assembly.FullName))
            .Options;
        IModelConfigurationContributor[] contributors =
        [
            new IdentityPlatformModelConfiguration(),
            new EducationAccountTopUpModelConfiguration(),
            new CourseBillingModelConfiguration(),
            new FasPaymentModelConfiguration()
        ];
        return new MoeDbContext(options, contributors);
    }
}
