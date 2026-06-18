using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moe.Infrastructure.Shared.Security;
using Moe.StudentFinance.Persistence;

namespace Moe.StudentFinance.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove the app's DbContext registration.
            services.RemoveAll(typeof(DbContextOptions<MoeDbContext>));
            services.RemoveAll(typeof(DbContextOptions));

            // Create a single shared internal service provider for all in-memory database contexts.
            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            // Add DbContext using an in-memory database for testing.
            services.AddDbContext<MoeDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting");
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            // Mock Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            
            services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
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

                options.AddPolicy(AuthorizationPolicies.ViewTopUps, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Permission, "TOPUPS_MANAGE", "TOPUP_VIEW_ALL");
                });
            });
        });
    }
}

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, 
        ILoggerFactory logger, UrlEncoder encoder) 
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = Request.Path.StartsWithSegments("/api/eservice", StringComparison.OrdinalIgnoreCase)
            ? [
                new Claim(ClaimTypes.Name, "Test Student"),
                new Claim(ClaimTypes.NameIdentifier, "test-student-id"),
                new Claim(ClaimNames.Portal, PortalCodes.EService),
                new Claim(ClaimNames.Role, "STUDENT"),
                new Claim(ClaimNames.PersonId, "2001"),
                new Claim(ClaimNames.UserAccountId, "1003")
            ]
            : [
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimNames.Portal, PortalCodes.Admin),
                new Claim(ClaimNames.Role, "SYSTEM_ADMIN"),
                new Claim(ClaimNames.Permission, "TOPUPS_MANAGE"),
                new Claim(ClaimNames.OrganizationUnitId, "1"),
                new Claim(ClaimNames.UserAccountId, "1001")
            ];
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        var result = AuthenticateResult.Success(ticket);

        return Task.FromResult(result);
    }
}
