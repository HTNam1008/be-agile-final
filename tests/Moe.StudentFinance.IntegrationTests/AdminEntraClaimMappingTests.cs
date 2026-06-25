using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moe.Application.Abstractions.Messaging;
using Moe.Infrastructure.Shared;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.IdentityPlatform.Api.Admin;
using Moe.SharedKernel.Results;
using Xunit;
using MoeAuthSchemes = Moe.Infrastructure.Shared.Security.AuthenticationSchemes;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class AdminEntraClaimMappingTests
{
    private const string AdminSigningKey = "MOE-dev-admin-local-token-signing-key-change-before-production-2026";

    [Fact]
    public void AdminEntra_preserves_short_claim_types_after_token_validation()
    {
        JwtBearerOptions options = CreateJwtOptions(MoeAuthSchemes.AdminEntra);
        string token = CreateAdminToken();

        ClaimsPrincipal principal = Validate(token, options);

        Assert.Contains(principal.Claims, claim => claim.Type == ClaimNames.Role && claim.Value == "HQ_ADMIN");
        Assert.DoesNotContain(principal.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "HQ_ADMIN");
        Assert.Contains(principal.Claims, claim => claim.Type == ClaimNames.Portal && claim.Value == PortalCodes.Admin);
        Assert.Contains(principal.Claims, claim => claim.Type == ClaimNames.Permission && claim.Value == "TOPUPS_MANAGE");
    }

    [Fact]
    public void AdminEntra_disables_inbound_mapping_without_changing_eservice_scheme()
    {
        using ServiceProvider provider = CreateSharedInfrastructureServices().BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();

        Assert.False(monitor.Get(MoeAuthSchemes.AdminEntra).MapInboundClaims);
        Assert.True(monitor.Get(MoeAuthSchemes.EServiceSingpass).MapInboundClaims);
    }

    [Fact]
    public async Task Auth_session_accepts_valid_dev_admin_token()
    {
        await using WebApplication app = await CreateAuthSessionAppAsync();
        using HttpClient client = app.GetTestClient();
        string token = CreateAdminToken();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/auth/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static JwtBearerOptions CreateJwtOptions(string scheme)
    {
        using ServiceProvider provider = CreateSharedInfrastructureServices().BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(scheme);
    }

    private static ServiceCollection CreateSharedInfrastructureServices()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = CreateConfiguration();
        services.AddLogging();
        services.AddSharedInfrastructure(configuration);
        return services;
    }

    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:AdminEntra:Authority"] = "https://login.microsoftonline.com/ea71ddeb-596c-4034-84d4-d65f91edc14a/v2.0",
                ["Authentication:AdminEntra:Audience"] = "api://dd6d290a-0fa8-4986-a699-2d14712d83c1",
                ["Authentication:AdminEntra:ClientId"] = "f289104d-7b05-41c4-8db9-16a0170d91a2",
                ["Authentication:AdminEntra:AllowedTenantId"] = "ea71ddeb-596c-4034-84d4-d65f91edc14a",
                ["Authentication:AdminEntra:RequireHttpsMetadata"] = "false",
                ["Authentication:AdminEntra:LocalTokenSigningKey"] = AdminSigningKey,
                ["Authentication:AdminEntra:LocalTokenLifetimeMinutes"] = "120",
                ["Authentication:EServiceSingpass:Authority"] = "http://localhost:5156/singpass/v3/fapi",
                ["Authentication:EServiceSingpass:Audience"] = "moe-eservice-api",
                ["Authentication:EServiceSingpass:ClientId"] = "moe-eservice-local",
                ["Authentication:EServiceSingpass:RequireHttpsMetadata"] = "false",
                ["Authentication:EServiceSingpass:LocalTokenSigningKey"] = "MOE-dev-eservice-local-token-signing-key-change-before-production-2026",
                ["Authentication:EServiceSingpass:LocalTokenLifetimeMinutes"] = "30",
                ["Authorization:UseStrictPermissionPolicies"] = "true"
            })
            .Build();

    private static string CreateAdminToken()
    {
        DateTime utcNow = DateTime.UtcNow;
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, "dev-admin-1"),
            new(JwtRegisteredClaimNames.Email, "system.admin@moe.local"),
            new(LocalIdentityClaimNames.Role, "HQ_ADMIN"),
            new(LocalIdentityClaimNames.Permission, "TOPUPS_MANAGE"),
            new(LocalIdentityClaimNames.Portal, PortalCodes.Admin)
        ];

        var token = new JwtSecurityToken(
            issuer: "https://login.microsoftonline.com/ea71ddeb-596c-4034-84d4-d65f91edc14a/v2.0",
            audience: "api://dd6d290a-0fa8-4986-a699-2d14712d83c1",
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.AddMinutes(120),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AdminSigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal Validate(string token, JwtBearerOptions options)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = options.MapInboundClaims
        };

        return handler.ValidateToken(token, options.TokenValidationParameters, out _);
    }

    private static async Task<WebApplication> CreateAuthSessionAppAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddConfiguration(CreateConfiguration());
        builder.Services.AddSharedInfrastructure(builder.Configuration);
        builder.Services.AddSingleton<IQueryDispatcher, ThrowingQueryDispatcher>();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminAuthController).Assembly);
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        }).AddMvc();

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseCors();
        app.UseSharedInfrastructure();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class ThrowingQueryDispatcher : IQueryDispatcher
    {
        public Task<Result<TResponse>> Send<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("The auth/session action does not dispatch queries.");
    }
}
