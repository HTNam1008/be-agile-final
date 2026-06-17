using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.IdentityModel.Tokens;
using Moe.Application.Abstractions.Modules;
using Moe.Infrastructure.Shared;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp;
using Moe.Modules.CourseBilling;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.FasPayment;
using Moe.StudentFinance.Persistence;
using Moe.Infrastructure.Shared.Validation;
using NSwag;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddLog4Net();

builder.Services.AddSharedInfrastructure(builder.Configuration);
builder.Services.AddMoePersistence(builder.Configuration);

IModule[] modules =
[
    new IdentityPlatformModule(),
    new EducationAccountTopUpModule(),
    new CourseBillingModule(),
    new FasPaymentModule()
];
foreach (var module in modules) module.AddServices(builder.Services, builder.Configuration);
builder.Services.AddSingleton<IReadOnlyCollection<IModule>>(modules);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<FluentValidationActionFilter>();
    })
    .AddApplicationPart(typeof(EducationAccountTopUpModule).Assembly)
    .AddApplicationPart(typeof(IdentityPlatformModule).Assembly);

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
    settings.PostProcess = document => ConfigureSwaggerArea(document, "/api/admin", "Admin");
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
    settings.PostProcess = document => ConfigureSwaggerArea(document, "/api/eservice", "E-Service");
});

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("UAT"))
{
    app.UseOpenApi();
    app.UseSwaggerUi(settings => settings.Path = "/swagger");
}

app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();

#if DEBUG
app.MapGet("/dev/admin-token", (IConfiguration configuration) =>
{
    string issuer = configuration["Authentication:AdminEntra:Authority"]?.TrimEnd('/')
        ?? throw new InvalidOperationException("Authentication:AdminEntra:Authority is required.");
    string audience = configuration["Authentication:AdminEntra:Audience"]
        ?? throw new InvalidOperationException("Authentication:AdminEntra:Audience is required.");
    string signingKey = configuration["Authentication:AdminEntra:LocalTokenSigningKey"]
        ?? throw new InvalidOperationException("Authentication:AdminEntra:LocalTokenSigningKey is required.");
    int lifetimeMinutes = configuration.GetValue("Authentication:AdminEntra:LocalTokenLifetimeMinutes", 120);
    DateTime utcNow = DateTime.UtcNow;

    Claim[] claims =
    [
        new(JwtRegisteredClaimNames.Sub, "dev-admin-1"),
        new(JwtRegisteredClaimNames.Email, "system.admin@moe.local"),
        new("name", "System Admin"),
        new("oid", "731f2a50-4fa7-4530-9294-1a5b912daf31"),
        new("tid", "ea71ddeb-596c-4034-84d4-d65f91edc14a"),
        new(LocalIdentityClaimNames.UserAccountId, "1"),
        new(LocalIdentityClaimNames.PersonId, "1"),
        new(LocalIdentityClaimNames.OrganizationUnitId, "1"),
        new(LocalIdentityClaimNames.Role, "SYSTEM_ADMIN"),
        new(LocalIdentityClaimNames.Permission, "TOPUPS_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "ACCOUNTS_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "ACCESS_SCOPE_MANAGE"),
        new(LocalIdentityClaimNames.Permission, "EXTERNAL_ACCOUNTS_PROVISION"),
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
#endif

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors();
app.UseSharedInfrastructure();
app.MapControllers();
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
foreach (var module in modules) module.MapEndpoints(app);
app.Run();

static void ConfigureSwaggerArea(OpenApiDocument document, string pathPrefix, string areaName)
{
    var pathsOutsideArea = document.Paths
        .Where(path => !path.Key.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
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

    if (path.Contains("/identity", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/admin-users", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/user-account", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/me", StringComparison.OrdinalIgnoreCase))
    {
        return "Identity";
    }

    return "General";
}

public partial class Program;
