using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Modules;
using Moe.Infrastructure.Shared;
using Moe.Infrastructure.Shared.Api;
using Moe.Infrastructure.Shared.Clock;
using Moe.Infrastructure.Shared.Security;
using Moe.Infrastructure.Shared.Validation;
using Moe.Modules.AiCopilot;
using Moe.Modules.CourseBilling;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.EducationAccountTopUp.IGateway.People;
using Moe.Modules.FasPayment;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.MailDelivery;
using Moe.Modules.Mfa;
using Moe.Modules.Notifications;
using Moe.StudentFinance.Api.CompositionRoot;
using Moe.StudentFinance.Persistence;
using NSwag;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddLog4Net();

builder.Services.AddSharedInfrastructure(builder.Configuration);
if (IsDevelopmentClockEnabled(builder.Environment, builder.Configuration))
{
    builder.Services.RemoveAll<IClock>();
    builder.Services.AddSingleton<DevelopmentManualClock>();
    builder.Services.AddSingleton<IClock>(sp => sp.GetRequiredService<DevelopmentManualClock>());
}
builder.Services.AddMoePersistence(builder.Configuration);
ConfigureSignalR(builder.Services, builder.Configuration);


IModule[] modules =
[
    new IdentityPlatformModule(),
    new EducationAccountTopUpModule(),
    new CourseBillingModule(),
    new FasPaymentModule(),
    new MfaModule(),
    new NotificationModule(),
    new AiCopilotModule(),
    new MailDeliveryModule()
];
foreach (var module in modules) module.AddServices(builder.Services, builder.Configuration);
builder.Services.AddScoped<IEligiblePersonLookupGateway, EligiblePersonLookupGatewayAdapter>();
builder.Services.AddScoped<ILifecyclePersonDisplayGateway, LifecyclePersonDisplayGatewayAdapter>();
builder.Services.AddSingleton<IReadOnlyCollection<IModule>>(modules);
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("PaymentCheckout", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("PublicFasSearch", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<FluentValidationActionFilter>();
    })
    .AddApplicationPart(typeof(EducationAccountTopUpModule).Assembly)
    .AddApplicationPart(typeof(CourseBillingModule).Assembly)
    .AddApplicationPart(typeof(IdentityPlatformModule).Assembly)
    .AddApplicationPart(typeof(FasPaymentModule).Assembly)
    .AddApplicationPart(typeof(MfaModule).Assembly)
    .AddApplicationPart(typeof(NotificationModule).Assembly)
    .AddApplicationPart(typeof(AiCopilotModule).Assembly)
    .AddApplicationPart(typeof(MailDeliveryModule).Assembly);

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        if (!context.ActionDescriptor.EndpointMetadata.OfType<UnprocessableEntityOnModelValidationAttribute>().Any())
        {
            return defaultFactory(context);
        }

        string[] errors = context.ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "Invalid request value." : error.ErrorMessage)
            .Distinct()
            .ToArray();
        return new Microsoft.AspNetCore.Mvc.ObjectResult(ApiResponse<object>.Fail(
            "Validation failed.",
            ["FAS.INVALID_REQUEST", .. errors],
            ApiResponseCodes.UnprocessableEntity,
            context.HttpContext.TraceIdentifier))
        {
            StatusCode = ApiResponseCodes.UnprocessableEntity
        };
    };
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "admin";
    settings.Title = "MOE Student Finance Admin API";
    settings.Version = "v1";
    settings.AddSecurity("Bearer", [], new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter an admin Microsoft Entra ID access token."
    });
    settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    settings.PostProcess = document => ConfigureSwaggerArea(document, "Admin", "/api/admin", "/api/mfa");
});

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "e-service";
    settings.Title = "MOE Student Finance E-Service API";
    settings.Version = "v1";
    settings.AddSecurity("Bearer", [], new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter an e-service Singpass or MockPass access token."
    });
    settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    settings.PostProcess = document => ConfigureSwaggerArea(document, "E-Service", "/api/eservice", "/api/mfa");
});

builder.Services.AddOpenApiDocument(settings =>
{
    settings.DocumentName = "mfa";
    settings.Title = "MOE Student Finance MFA API";
    settings.Version = "v1";
    settings.AddSecurity("Bearer", [], new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter an admin or e-service access token for authenticated MFA operations."
    });
    settings.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
    settings.PostProcess = document => ConfigureSwaggerArea(document, "MFA", "/api/mfa");
});

var app = builder.Build();
app.Logger.LogInformation(
    "Data Protection keys are persisted to {DataProtectionKeysPath}",
    Moe.Infrastructure.Shared.DependencyInjection.ResolveDataProtectionKeysDirectory().FullName);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Moe.StudentFinance.Persistence.MoeDbContext>();
    if (db.Database.IsSqlite())
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
}

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("UAT"))
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings => settings.Path = "/swagger");
}

//app.MapGet("/", () => Results.Ok(new
//{
//    service = "MOE Student Finance API",
//    status = "running"
//})).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

app.MapGet("/dev/admin-token", (IConfiguration configuration) =>
{
    string? issuer = configuration["Authentication:AdminEntra:Authority"]?.TrimEnd('/');
    string? audience = configuration["Authentication:AdminEntra:Audience"];
    string? signingKey = configuration["Authentication:AdminEntra:LocalTokenSigningKey"];

    if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(signingKey))
    {
        return Results.BadRequest(new
        {
            error = "DEV_ADMIN_TOKEN_UNAVAILABLE",
            message = "LocalTokenSigningKey is not configured. Admin auth uses real Entra ID."
        });
    }

    int lifetimeMinutes = configuration.GetValue("Authentication:AdminEntra:LocalTokenLifetimeMinutes", 120);
    DateTime utcNow = DateTime.UtcNow;

    Claim[] claims =
    [
        new(JwtRegisteredClaimNames.Sub, "dev-admin-1"),
        new(JwtRegisteredClaimNames.Email, "system.admin@moe.local"),
        new("name", "HQ Admin"),
        new("oid", "731f2a50-4fa7-4530-9294-1a5b912daf31"),
        new("tid", "ea71ddeb-596c-4034-84d4-d65f91edc14a"),
        new(LocalIdentityClaimNames.UserAccountId, "1002"),
        new(LocalIdentityClaimNames.PersonId, "1"),
        new(LocalIdentityClaimNames.OrganizationUnitId, "1"),
        new(LocalIdentityClaimNames.Role, "HQ_ADMIN"),
        new(LocalIdentityClaimNames.Permission, "TOPUPS_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "ACCOUNTS_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "LIFECYCLE_MANUAL_TRIGGER"),
        new(LocalIdentityClaimNames.Permission, "ACCESS_SCOPE_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "EXTERNAL_ACCOUNTS_PROVISION"),
        new(LocalIdentityClaimNames.Permission, "FAS_SCHEME_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "FAS_REVIEW"),
        new(LocalIdentityClaimNames.Permission, "AI_REVIEW_MANAGE"),
        new(LocalIdentityClaimNames.Portal, PortalCodes.Admin),
        new(LocalIdentityClaimNames.IdentityProvider, "ENTRA_WORKFORCE")
    ];

    JwtSecurityToken token = new(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: utcNow,
        expires: utcNow.AddMinutes(lifetimeMinutes),
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256));

    string accessToken = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new
    {
        tokenType = "Bearer",
        accessToken,
        expiresAtUtc = utcNow.AddMinutes(lifetimeMinutes)
    });
}).AllowAnonymous();

if (IsDevelopmentClockEnabled(app.Environment, app.Configuration))
{
    app.MapGet("/dev/clock", ([FromServices] DevelopmentManualClock clock) => Results.Ok(CreateDevelopmentClockResponse(clock)))
        .AllowAnonymous()
        .RequireCors("PortalCors");

    app.MapPut("/dev/clock", (SetDevelopmentClockRequest request, [FromServices] DevelopmentManualClock clock) =>
    {
        clock.Set(request.UtcNow);
        return Results.Ok(CreateDevelopmentClockResponse(clock));
    })
        .AllowAnonymous()
        .RequireCors("PortalCors");

    app.MapPost("/dev/clock/advance", (AdvanceDevelopmentClockRequest request, [FromServices] DevelopmentManualClock clock) =>
    {
        TimeSpan delta = request.ToTimeSpan();
        if (delta == TimeSpan.Zero)
        {
            return Results.BadRequest(new
            {
                error = "DEV_CLOCK_ADVANCE_ZERO",
                message = "At least one advance component must be non-zero."
            });
        }

        clock.Advance(delta);
        return Results.Ok(CreateDevelopmentClockResponse(clock));
    })
        .AllowAnonymous()
        .RequireCors("PortalCors");

    app.MapDelete("/dev/clock", ([FromServices] DevelopmentManualClock clock) =>
    {
        clock.Reset();
        return Results.Ok(CreateDevelopmentClockResponse(clock));
    })
        .AllowAnonymous()
        .RequireCors("PortalCors");
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseSharedInfrastructure();
app.MapControllers();
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
foreach (var module in modules) module.MapEndpoints(app);
app.Run();

static void ConfigureSwaggerArea(OpenApiDocument document, string areaName, params string[] pathPrefixes)
{
    var pathsOutsideArea = document.Paths
        .Where(path => !pathPrefixes.Any(prefix => path.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        .Select(path => path.Key)
        .ToArray();

    foreach (string path in pathsOutsideArea)
    {
        document.Paths.Remove(path);
    }

    foreach (var path in document.Paths)
    {
        foreach (var operation in path.Value.Values)
        {
            operation.Tags.Clear();
            operation.Tags.Add(GetSwaggerTag(path.Key));
        }
    }

    document.Tags.Clear();
    foreach (string tagName in document.Paths
        .SelectMany(path => path.Value.Values)
        .SelectMany(operation => operation.Tags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
    {
        document.Tags.Add(new OpenApiTag { Name = tagName });
    }

    document.Info.Title = $"MOE Student Finance {areaName} API";
}

static string GetSwaggerTag(string path)
{
    if (path.Contains("/access-scopes", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/user-access-scopes", StringComparison.OrdinalIgnoreCase))
    {
        return "Access";
    }

    if (path.Contains("/education-account", StringComparison.OrdinalIgnoreCase))
    {
        return "Accounts";
    }

    if (path.Contains("/courses", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/course-enrollments", StringComparison.OrdinalIgnoreCase))
    {
        return "Courses";
    }

    if (path.Contains("/top-up", StringComparison.OrdinalIgnoreCase))
    {
        return "Top up";
    }

    if (path.Contains("/identity", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/admin-users", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/students", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/user-account", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/me", StringComparison.OrdinalIgnoreCase))
    {
        return "Identity";
    }

    if (path.Contains("/mfa", StringComparison.OrdinalIgnoreCase))
    {
        return "MFA";
    }

    return "General";
}

internal sealed record SetDevelopmentClockRequest(DateTimeOffset UtcNow);

internal sealed record AdvanceDevelopmentClockRequest(int Days = 0, int Hours = 0, int Minutes = 0, int Seconds = 0)
{
    public TimeSpan ToTimeSpan() => new(Days, Hours, Minutes, Seconds);
}

internal sealed record DevelopmentClockResponse(DateTimeOffset UtcNow, DateOnly UtcDate, bool IsOverridden);

public partial class Program
{
    internal static bool IsDevelopmentClockEnabled(IHostEnvironment _, IConfiguration configuration)
        => configuration.GetValue("DevTools:Clock:Enabled", false);

    internal static DevelopmentClockResponse CreateDevelopmentClockResponse(DevelopmentManualClock clock)
        => new(clock.UtcNow, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), clock.IsOverridden);

    internal static void ConfigureSignalR(IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["SignalR:Provider"]?.Trim() ?? "Local";
        ISignalRServerBuilder signalR = services.AddSignalR();

        if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported SignalR provider '{provider}'. Use 'Local' or 'Azure'.");
        }

        string? connectionString =
            configuration["SignalR:AzureConnectionString"]
            ?? configuration["Azure:SignalR:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "SignalR Azure provider requires SignalR:AzureConnectionString or Azure:SignalR:ConnectionString.");
        }

        signalR.AddAzureSignalR(connectionString);
    }
}
