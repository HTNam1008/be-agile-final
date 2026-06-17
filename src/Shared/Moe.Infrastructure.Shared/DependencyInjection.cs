using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Clock;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Messaging;
using Moe.Infrastructure.Shared.Middleware;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Infrastructure.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddProblemDetails();
        services.AddHealthChecks();

        services.AddOptions<PortalOptions>().BindConfiguration(PortalOptions.SectionName).ValidateOnStart();
        services.AddOptions<AuthenticationOptions>().BindConfiguration(AuthenticationOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<UatOptions>().BindConfiguration(UatOptions.SectionName).ValidateOnStart();

        var auth = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>() ?? new();
        services.AddAuthentication()
            .AddJwtBearer(AuthenticationSchemes.AdminEntra, options => Bind(options, auth.AdminEntra, AuthenticationSchemes.AdminEntra))
            .AddJwtBearer(AuthenticationSchemes.EServiceSingpass, options => Bind(options, auth.EServiceSingpass, AuthenticationSchemes.EServiceSingpass));

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminPortal, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.AdminEntra);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNames.Portal, PortalCodes.Admin);
            });
            options.AddPolicy(AuthorizationPolicies.EServicePortal, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.EServiceSingpass);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNames.Portal, PortalCodes.EService);
            });
            AddPermission(options, AuthorizationPolicies.ManageAccessScopes, "ACCESS_SCOPE_MANAGE");
            AddPermission(options, AuthorizationPolicies.ManageAccounts, "ACCOUNTS_MANAGE");
            AddPermission(options, AuthorizationPolicies.ManageExternalAccounts, "EXTERNAL_ACCOUNTS_PROVISION");
            AddPermission(options, AuthorizationPolicies.ManageTopUps, "TOPUPS_MANAGE");
            AddPermission(options, AuthorizationPolicies.ManageCourses, "COURSES_MANAGE");
            AddPermission(options, AuthorizationPolicies.ReviewFas, "FAS_REVIEW");
        });

        services.AddCors(options =>
        {
            var portals = configuration.GetSection(PortalOptions.SectionName).Get<PortalOptions>() ?? new();
            options.AddPolicy("AdminCors", p => p.WithOrigins(portals.AdminAllowedOrigins).AllowAnyHeader().AllowAnyMethod());
            options.AddPolicy("EServiceCors", p => p.WithOrigins(portals.EServiceAllowedOrigins).AllowAnyHeader().AllowAnyMethod());
        });
        return services;
    }

    public static WebApplication UseSharedInfrastructure(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<ApiVersionHeaderMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<PerformanceTrackingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static void Bind(JwtBearerOptions target, JwtSchemeOptions source, string authenticationScheme)
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(source.LocalTokenSigningKey))
        {
            target.RequireHttpsMetadata = source.RequireHttpsMetadata;
            target.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = source.Authority.TrimEnd('/'),
                ValidateAudience = true,
                ValidAudience = source.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(source.LocalTokenSigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            target.Events = CreateSchemeEvents(authenticationScheme);
            return;
        }
#endif

        target.Authority = source.Authority;
        target.Audience = source.Audience;
        target.RequireHttpsMetadata = source.RequireHttpsMetadata;
        target.Events = CreateSchemeEvents(authenticationScheme);
        ConfigureTenantIssuers(target, source);
    }

    private static void Bind(JwtBearerOptions target, SingpassSchemeOptions source, string authenticationScheme)
    {
        if (!string.IsNullOrWhiteSpace(source.LocalTokenSigningKey))
        {
            target.RequireHttpsMetadata = source.RequireHttpsMetadata;
            target.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = source.Authority.TrimEnd('/'),
                ValidateAudience = true,
                ValidAudience = source.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(source.LocalTokenSigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            target.Events = CreateSchemeEvents(authenticationScheme);
            return;
        }

        target.Authority = source.Authority;
        target.Audience = source.Audience;
        target.RequireHttpsMetadata = source.RequireHttpsMetadata;
        target.Events = CreateSchemeEvents(authenticationScheme);

        if (!string.IsNullOrWhiteSpace(source.DiscoveryEndpoint))
        {
            target.MetadataAddress = source.DiscoveryEndpoint;
        }
    }

    private static void ConfigureTenantIssuers(JwtBearerOptions target, JwtSchemeOptions source)
    {
        if (string.IsNullOrWhiteSpace(source.AllowedTenantId))
        {
            return;
        }

        string tenantId = source.AllowedTenantId.Trim();
        string authority = source.Authority.TrimEnd('/');
        string authorityV2Issuer = authority.EndsWith("/v2.0", StringComparison.OrdinalIgnoreCase)
            ? authority
            : $"{authority}/v2.0";

        target.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuers =
            [
                authorityV2Issuer,
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/"
            ]
        };
    }

    private static JwtBearerEvents CreateSchemeEvents(string authenticationScheme)
    {
        return new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    identity.AddClaim(new Claim(LocalIdentityClaimNames.ExternalAuthenticationScheme, authenticationScheme));
                }

                return Task.CompletedTask;
            }
        };
    }

    private static void AddPermission(Microsoft.AspNetCore.Authorization.AuthorizationOptions options, string policyName, string permission)
        => options.AddPolicy(policyName, policy =>
        {
            policy.AddAuthenticationSchemes(AuthenticationSchemes.AdminEntra);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(ClaimNames.Permission, permission);
        });
}
