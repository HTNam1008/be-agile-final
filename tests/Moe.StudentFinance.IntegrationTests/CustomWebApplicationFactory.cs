using System.Security.Claims;
using System.Text.Json;
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
using Microsoft.Extensions.Hosting;
using Moe.Infrastructure.Shared.Security;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.FasPayment.IGateway.Payments;
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
            services.AddHostedService<IntegrationTestDbSeeder>();
            services.RemoveAll<IStripePaymentGateway>();
            services.AddSingleton<IStripePaymentGateway, IntegrationTestStripeGateway>();

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

                options.AddPolicy(AuthorizationPolicies.EServicePortal, policy =>
                {
                    policy.AuthenticationSchemes.Clear();
                    policy.AddAuthenticationSchemes("Test");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(ClaimNames.Portal, PortalCodes.EService);
                    policy.RequireClaim(ClaimNames.Role, "STUDENT");
                });
            });
        });
    }
}

internal sealed class IntegrationTestStripeGateway : IStripePaymentGateway
{
    public Task<StripeCheckoutGatewayResult> CreateCheckoutAsync(
        StripeCheckoutGatewayRequest request,
        CancellationToken cancellationToken)
        => Task.FromResult(new StripeCheckoutGatewayResult(
            $"cs_test_{request.CheckoutId}",
            request.ProviderPriceId ?? $"price_test_{request.CheckoutId}",
            $"https://stripe.test/checkout/{request.CheckoutId}",
            request.ExpiresAtUtc));

    public Task ExpireCheckoutAsync(
        string providerSessionId,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<StripeScheduleGatewayResult> AttachFiniteScheduleAsync(
        string providerSubscriptionId,
        string providerPriceId,
        int installmentCount,
        CancellationToken cancellationToken)
        => Task.FromResult(new StripeScheduleGatewayResult(
            $"schedule_test_{providerSubscriptionId}"));

    public ParsedPaymentWebhook ParseWebhook(string payload, string signatureHeader)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        string kind = root.GetProperty("kind").GetString()!;
        PaymentWebhookKind webhookKind = kind switch
        {
            "success" => PaymentWebhookKind.PaymentSucceeded,
            "failure" => PaymentWebhookKind.PaymentFailed,
            "expired" => PaymentWebhookKind.CheckoutExpired,
            "refund" => PaymentWebhookKind.ChargeRefunded,
            _ => PaymentWebhookKind.Ignored
        };
        long checkoutId = root.TryGetProperty("checkoutId", out JsonElement checkoutElement)
            ? checkoutElement.GetInt64()
            : 0;
        long amountMinor = root.TryGetProperty("amountMinor", out JsonElement amount)
            ? amount.GetInt64()
            : 0;
        DateTime createdAtUtc = root.TryGetProperty("createdAtUtc", out JsonElement created)
            ? created.GetDateTime()
            : DateTime.UtcNow;
        string eventId = root.TryGetProperty("eventId", out JsonElement eventElement)
            ? eventElement.GetString()!
            : $"evt_test_{Guid.NewGuid():N}";

        return new ParsedPaymentWebhook(
            eventId,
            webhookKind switch
            {
                PaymentWebhookKind.PaymentSucceeded => "payment_intent.succeeded",
                PaymentWebhookKind.PaymentFailed => "payment_intent.payment_failed",
                PaymentWebhookKind.CheckoutExpired => "checkout.session.expired",
                PaymentWebhookKind.ChargeRefunded => "charge.refunded",
                _ => "test.ignored"
            },
            webhookKind,
            createdAtUtc,
            checkoutId,
            $"cs_test_{checkoutId}",
            $"pi_test_{checkoutId}",
            null,
            $"ch_test_{checkoutId}",
            null,
            amountMinor,
            "sgd");
    }

    public Task<StripeRefundGatewayResult> CreateRefundAsync(
        string idempotencyKey,
        string providerChargeId,
        long amountMinor,
        CancellationToken cancellationToken)
        => Task.FromResult(new StripeRefundGatewayResult(
            $"re_test_{Guid.NewGuid():N}"));

}

internal sealed class IntegrationTestDbSeeder(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SeedDemoSchool(db);
        SeedStudent(db, 2101, "IT-STU-0001", "Integration Student One", new DateOnly(2008, 2, 10), "SEC_4", "4A", 1);
        SeedStudent(db, 2102, "IT-STU-0002", "Integration Student Two", new DateOnly(2009, 7, 15), "SEC_3", "3B", 1);
        SeedAccount(db, 2101, "EA-IT-0001", 125.00m);
        SeedAccount(db, 2102, "EA-IT-0002", 225.00m);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void SeedDemoSchool(MoeDbContext db)
    {
        Type schoolType = typeof(Person).Assembly.GetType(
            "Moe.Modules.IdentityPlatform.Domain.Iam.OrganizationUnit",
            throwOnError: true)!;

        if (db.Find(schoolType, 1L) is null)
        {
            object school = Activator.CreateInstance(
                schoolType,
                "MOEDEMO",
                "Demo Secondary School",
                "SCHOOL",
                DateTime.UtcNow)!;

            SetId(school, 1);
            db.Add(school);
        }

        if (db.Find(schoolType, 2L) is not null)
        {
            return;
        }

        object otherSchool = Activator.CreateInstance(
            schoolType,
            "MOEOTHER",
            "Other Secondary School",
            "SCHOOL",
            DateTime.UtcNow)!;

        SetId(otherSchool, 2);
        db.Add(otherSchool);
    }

    private static void SeedStudent(
        MoeDbContext db,
        long personId,
        string studentNumber,
        string fullName,
        DateOnly dateOfBirth,
        string levelCode,
        string classCode,
        long organizationId)
    {
        if (!db.Set<Person>().Any(x => x.Id == personId))
        {
            db.Set<Person>().Add(new Person(
                personId,
                $"IT-PERSON-{personId}",
                fullName,
                dateOfBirth,
                "SG",
                "CITIZEN"));
        }

        if (db.Set<SchoolEnrollment>().Any(x => x.PersonId == personId && x.AcademicYear == "2026"))
        {
            return;
        }

        SchoolEnrollment enrollment = (SchoolEnrollment)Activator.CreateInstance(typeof(SchoolEnrollment), nonPublic: true)!;
        SetId(enrollment, 9000 + personId);
        SetProperty(enrollment, nameof(SchoolEnrollment.PersonId), personId);
        SetProperty(enrollment, nameof(SchoolEnrollment.OrganizationId), organizationId);
        SetProperty(enrollment, nameof(SchoolEnrollment.StudentNumber), studentNumber);
        SetProperty(enrollment, nameof(SchoolEnrollment.AcademicYear), "2026");
        SetProperty(enrollment, nameof(SchoolEnrollment.LevelCode), levelCode);
        SetProperty(enrollment, nameof(SchoolEnrollment.ClassCode), classCode);
        SetProperty(enrollment, nameof(SchoolEnrollment.SchoolingStatusCode), "ACTIVE");
        SetProperty(enrollment, nameof(SchoolEnrollment.StatusReasonCode), null);
        SetProperty(enrollment, nameof(SchoolEnrollment.StartDate), new DateOnly(2026, 1, 2));
        SetProperty(enrollment, nameof(SchoolEnrollment.EndDate), null);
        SetProperty(enrollment, nameof(SchoolEnrollment.SourceCode), "INTEGRATION_TEST");
        SetProperty(enrollment, nameof(SchoolEnrollment.CreatedAtUtc), DateTime.UtcNow);
        SetProperty(enrollment, nameof(SchoolEnrollment.UpdatedAtUtc), DateTime.UtcNow);
        db.Set<SchoolEnrollment>().Add(enrollment);
    }

    private static void SeedAccount(MoeDbContext db, long personId, string accountNumber, decimal balance)
    {
        if (db.Set<EducationAccount>().Any(x => x.PersonId == personId))
        {
            return;
        }

        var accountResult = EducationAccount.OpenManual(
            personId,
            accountNumber,
            DateTimeOffset.UtcNow,
            "Integration Test Seed",
            "Integration account seed",
            1001);

        if (!accountResult.IsSuccess)
        {
            return;
        }

        accountResult.Value.UpdateBalance(balance);
        db.Set<EducationAccount>().Add(accountResult.Value);
    }

    private static void SetId(object entity, long id)
    {
        entity.GetType().GetProperty("Id")!.SetValue(entity, id);
    }

    private static void SetProperty(object entity, string propertyName, object? value)
    {
        entity.GetType().GetProperty(propertyName)!.SetValue(entity, value);
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
        string requestedRole = Request.Headers.TryGetValue("X-Test-Role", out var values)
            ? values.ToString()
            : "SCHOOL_ADMIN";

        Claim[] claims = Request.Path.StartsWithSegments("/api/eservice", StringComparison.OrdinalIgnoreCase)
            ? [
                new Claim(ClaimTypes.Name, "Test Student"),
                new Claim(ClaimTypes.NameIdentifier, "test-student-id"),
                new Claim(ClaimNames.Portal, PortalCodes.EService),
                new Claim(ClaimNames.Role, "STUDENT"),
                new Claim(ClaimNames.PersonId, GetHeaderValue("X-Test-PersonId", "2001")),
                new Claim(ClaimNames.UserAccountId, GetHeaderValue("X-Test-UserAccountId", "1003"))
            ]
            : [
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimNames.Portal, PortalCodes.Admin),
                new Claim(ClaimNames.Role, requestedRole),
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

    private string GetHeaderValue(string name, string fallback)
    {
        return Request.Headers.TryGetValue(name, out var values) && !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : fallback;
    }
}
