using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moe.Infrastructure.Shared.Security;
using Moe.StudentFinance.Persistence;
using Moe.StudentFinance.IntegrationTests; // To re-use TestAuthHandler

[assembly: HostingStartup(typeof(Moe.StudentFinance.E2EHost.E2EHostingStartup))]

namespace Moe.StudentFinance.E2EHost;

public class E2EHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // 1. Remove original DbContext and use InMemory
            services.RemoveAll(typeof(DbContextOptions<MoeDbContext>));
            services.RemoveAll(typeof(DbContextOptions));

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<MoeDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForE2E");
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            // 2. Mock Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

            services.PostConfigure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            {
                options.AddPolicy(AuthorizationPolicies.AdminPortal, policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Portal, PortalCodes.Admin);
                });

                options.AddPolicy(AuthorizationPolicies.ManageTopUps, policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "TOPUPS_MANAGE");
                });

                options.AddPolicy(AuthorizationPolicies.ViewTopUps, policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "TOPUPS_MANAGE", "TOPUP_VIEW_ALL");
                });
            });
        });

        // 3. Register the DB Seeder
        builder.ConfigureServices(services => 
        {
            services.AddHostedService<E2EDbSeeder>();
        });
    }
}
