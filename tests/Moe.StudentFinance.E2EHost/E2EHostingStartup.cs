using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moe.Infrastructure.Shared.Security;
using Moe.StudentFinance.IntegrationTests; // To re-use TestAuthHandler
using Moe.StudentFinance.Persistence;

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
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Portal, PortalCodes.Admin);
                });

                options.AddPolicy(AuthorizationPolicies.ManageTopUps, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "TOPUPS_MANAGE");
                });

                options.AddPolicy(AuthorizationPolicies.EServicePortal, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Portal, PortalCodes.EService);
                    policy.RequireClaim(ClaimNames.Role, "STUDENT");
                });

                options.AddPolicy(AuthorizationPolicies.MfaPortal, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context =>
                        (context.User.HasClaim(ClaimNames.Portal, PortalCodes.Admin)
                            && context.User.FindAll(ClaimNames.Role).Any(claim => claim.Value is "HQ_ADMIN" or "SCHOOL_ADMIN"))
                        || (context.User.HasClaim(ClaimNames.Portal, PortalCodes.EService)
                            && context.User.HasClaim(ClaimNames.Role, "STUDENT")));
                });

                options.AddPolicy(AuthorizationPolicies.ManageFasSchemes, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "FAS_SCHEME_MANAGE");
                });

                options.AddPolicy(AuthorizationPolicies.ReviewFas, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "FAS_REVIEW");
                });

                options.AddPolicy(AuthorizationPolicies.ViewTopUps, policy =>
                {
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "TOPUPS_MANAGE", "TOPUP_VIEW_ALL");
                });

                options.AddPolicy(AuthorizationPolicies.LifecycleManualTrigger, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "LIFECYCLE_MANUAL_TRIGGER");
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
