using Asp.Versioning;
using Moe.Application.Abstractions.Modules;
using Moe.Infrastructure.Shared;
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
