using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Messaging;
using Moe.Application.Abstractions.Security;
using Moe.Infrastructure.Shared.Clock;
using Moe.Infrastructure.Shared.Configuration;
using Moe.Infrastructure.Shared.Messaging;
using Moe.Infrastructure.Shared.Middleware;
using Moe.Infrastructure.Shared.Observability;
using Moe.Infrastructure.Shared.Security;

namespace Moe.Infrastructure.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdDelegatingHandler>();
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add(
                    builder.Services.GetRequiredService<CorrelationIdDelegatingHandler>());
            });
        });
        services.AddDataProtection()
            .PersistKeysToFileSystem(ResolveDataProtectionKeysDirectory())
            .SetApplicationName("Moe.StudentFinance");
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<IAdminAccessControl, AdminAccessControl>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddProblemDetails();
        services.AddHealthChecks();

        services.AddOptions<PortalOptions>().BindConfiguration(PortalOptions.SectionName).ValidateOnStart();
        services.AddOptions<AuthenticationOptions>()
            .BindConfiguration(AuthenticationOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(HasValidEServiceSingpassRedirects, "Authentication:EServiceSingpass:RedirectUri must point at the API callback, not the frontend dev server.")
            .ValidateOnStart();
        services.AddOptions<Configuration.AuthorizationOptions>().BindConfiguration(Configuration.AuthorizationOptions.SectionName).ValidateOnStart();
        services.AddOptions<UatOptions>().BindConfiguration(UatOptions.SectionName).ValidateOnStart();

        var auth = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>() ?? new();
        var authorization = configuration.GetSection(Configuration.AuthorizationOptions.SectionName).Get<Configuration.AuthorizationOptions>() ?? new();
        services.AddAuthentication()
            .AddJwtBearer(AuthenticationSchemes.AdminEntra, options => Bind(options, auth.AdminEntra, AuthenticationSchemes.AdminEntra, AuthenticationCookies.AdminSession))
            .AddJwtBearer(AuthenticationSchemes.EServiceSingpass, options => Bind(options, auth.EServiceSingpass, AuthenticationSchemes.EServiceSingpass));

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminPortal, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.AdminEntra);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNames.Portal, PortalCodes.Admin);
                policy.RequireClaim(ClaimNames.Role, "HQ_ADMIN", "SCHOOL_ADMIN");
            });
            options.AddPolicy(AuthorizationPolicies.EServicePortal, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.EServiceSingpass);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ClaimNames.Portal, PortalCodes.EService);
                policy.RequireClaim(ClaimNames.Role, "STUDENT");
            });
            options.AddPolicy(AuthorizationPolicies.MfaPortal, policy =>
            {
                policy.AddAuthenticationSchemes(AuthenticationSchemes.AdminEntra, AuthenticationSchemes.EServiceSingpass);
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                {
                    bool isAdmin = context.User.HasClaim(ClaimNames.Portal, PortalCodes.Admin)
                        && context.User.FindAll(ClaimNames.Role).Any(claim => claim.Value is "HQ_ADMIN" or "SCHOOL_ADMIN");

                    bool isEService = context.User.HasClaim(ClaimNames.Portal, PortalCodes.EService)
                        && context.User.HasClaim(ClaimNames.Role, "STUDENT");

                    return isAdmin || isEService;
                });
            });
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageAccessScopes, "ACCESS_SCOPE_MANAGE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageAccounts, "ACCOUNT_MANUAL_CREATE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(
                options,
                AuthorizationPolicies.ViewAccountDetails,
                ["ACCOUNT_VIEW_ALL", "ACCOUNT_VIEW_SCHOOL"],
                authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageAccountDetails, "ACCOUNT_DETAILS_MANAGE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageAccountLifecycle, "ACCOUNT_LIFECYCLE_MANAGE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.LifecycleManualTrigger, "LIFECYCLE_MANUAL_TRIGGER", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageExternalAccounts, "EXTERNAL_ACCOUNTS_PROVISION", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageTopUps, "TOPUPS_MANAGE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(
                options,
                AuthorizationPolicies.ViewTopUps,
                ["TOPUPS_MANAGE", "TOPUP_VIEW_ALL"],
                authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageCourses, "COURSE_MANAGE_OWN_SCHOOL", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ReviewFas, "FAS_REVIEW", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageFasSchemes, "FAS_SCHEME_MANAGE", authorization.UseStrictPermissionPolicies);
            AddAdminFeaturePolicy(options, AuthorizationPolicies.ManageAiReviews, "AI_REVIEW_MANAGE", authorization.UseStrictPermissionPolicies);
        });

        services.AddCors(options =>
        {
            var portals = configuration.GetSection(PortalOptions.SectionName).Get<PortalOptions>() ?? new();
            options.AddPolicy("AdminCors", p => p.WithOrigins(portals.AdminAllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Response-Time-Ms", "X-Correlation-ID"));
            options.AddPolicy("EServiceCors", p => p.WithOrigins(portals.EServiceAllowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Response-Time-Ms", "X-Correlation-ID"));
            options.AddPolicy("PortalCors", p => p
                .WithOrigins(portals.AdminAllowedOrigins.Concat(portals.EServiceAllowedOrigins).Distinct().ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("X-Response-Time-Ms", "X-Correlation-ID"));
        });
        return services;
    }

    public static DirectoryInfo ResolveDataProtectionKeysDirectory()
    {
        string?[] candidateBasePaths =
        [
            Environment.GetEnvironmentVariable("HOME"),
            Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            Path.GetTempPath(),
            AppContext.BaseDirectory
        ];

        foreach (string? candidateBasePath in candidateBasePaths)
        {
            if (string.IsNullOrWhiteSpace(candidateBasePath))
            {
                continue;
            }

            string keysPath = Path.Combine(candidateBasePath, "ASP.NET", "DataProtection-Keys");

            try
            {
                return Directory.CreateDirectory(keysPath);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        throw new InvalidOperationException("A writable Data Protection keys directory could not be resolved.");
    }

    public static WebApplication UseSharedInfrastructure(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<ApiVersionHeaderMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<UserContextLoggingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<PerformanceTrackingMiddleware>();
        app.UseAuthorization();
        return app;
    }

    private static bool HasValidEServiceSingpassRedirects(AuthenticationOptions options)
    {
        string redirectUri = options.EServiceSingpass.RedirectUri;

        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return true;
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? callback))
        {
            return false;
        }

        if (!string.Equals(callback.AbsolutePath, "/api/eservice/v1/auth/callback", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return callback.Port is not 5173 and not 3000;
    }

    private static void Bind(JwtBearerOptions target, JwtSchemeOptions source, string authenticationScheme, string? bearerCookieName = null)
    {
        target.MapInboundClaims = false;

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
            target.Events = CreateSchemeEvents(authenticationScheme, bearerCookieName);
            return;
        }

        target.Authority = source.Authority;
        target.Audience = source.Audience;
        target.RequireHttpsMetadata = source.RequireHttpsMetadata;
        target.Events = CreateSchemeEvents(authenticationScheme, bearerCookieName);
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
            target.Events = CreateSchemeEvents(authenticationScheme, AuthenticationCookies.EServiceSession);
            return;
        }

        target.Authority = source.Authority;
        target.Audience = source.Audience;
        target.RequireHttpsMetadata = source.RequireHttpsMetadata;
        target.Events = CreateSchemeEvents(authenticationScheme, AuthenticationCookies.EServiceSession);

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

    private static JwtBearerEvents CreateSchemeEvents(string authenticationScheme, string? bearerCookieName = null)
    {
        return new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (authenticationScheme == AuthenticationSchemes.AdminEntra
                    && IsAdminSessionEstablishmentRequest(context.Request))
                {
                    return Task.CompletedTask;
                }

                if (string.IsNullOrWhiteSpace(context.Token)
                    && !string.IsNullOrWhiteSpace(bearerCookieName)
                    && context.Request.Cookies.TryGetValue(bearerCookieName, out string? cookieToken))
                {
                    context.Token = cookieToken;
                }
                else if (authenticationScheme == AuthenticationSchemes.AdminEntra
                    && context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    context.NoResult();
                }

                return Task.CompletedTask;
            },
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

    private static bool IsAdminSessionEstablishmentRequest(HttpRequest request)
        => HttpMethods.IsPost(request.Method)
            && request.Path.Value is string path
            && path.StartsWith("/api/admin/v", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/auth/session", StringComparison.OrdinalIgnoreCase);

    private static void AddAdminFeaturePolicy(
        Microsoft.AspNetCore.Authorization.AuthorizationOptions options,
        string policyName,
        string permission,
        bool useStrictPermissionPolicies)
        => AddAdminFeaturePolicy(options, policyName, [permission], useStrictPermissionPolicies);

    private static void AddAdminFeaturePolicy(
        Microsoft.AspNetCore.Authorization.AuthorizationOptions options,
        string policyName,
        IReadOnlyCollection<string> permissions,
        bool useStrictPermissionPolicies)
        => options.AddPolicy(policyName, policy =>
        {
            policy.AddAuthenticationSchemes(AuthenticationSchemes.AdminEntra);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(ClaimNames.Portal, PortalCodes.Admin);
            policy.RequireClaim(ClaimNames.Role, "HQ_ADMIN", "SCHOOL_ADMIN");

            if (useStrictPermissionPolicies)
            {
                policy.RequireClaim(ClaimNames.Permission, permissions);
            }
        });
}
