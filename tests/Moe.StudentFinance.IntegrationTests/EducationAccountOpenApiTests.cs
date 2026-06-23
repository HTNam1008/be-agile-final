using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moe.Application.Abstractions.Audit;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.StudentFinance.IntegrationTests;

public sealed class EducationAccountOpenApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenManualEndpoint_Should_Allow_Standard_Admin_Account_Management_Permission()
    {
        long personId = await SeedPersonWithoutAccountAsync();

        using HttpResponseMessage response = await _client.PostAsJsonAsync(
            "/api/admin/v1/education-accounts",
            new
            {
                personId,
                reasonCode = "EXCEPTION",
                remarks = "Manual integration test open account"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        EducationAccount account = await db.Set<EducationAccount>().SingleAsync(x => x.PersonId == personId);
        Assert.Equal("ACTIVE", account.StatusCode);

        object auditLog = SingleEntity(
            db,
            "Moe.Modules.IdentityPlatform.Domain.Audit.AuditLog",
            x => (string)GetProperty(x, "ActionCode")! == AuditActionCodes.EducationAccountCreatedManually
                && (string)GetProperty(x, "EntityTypeCode")! == "EducationAccount"
                && (long?)GetProperty(x, "EntityId") == account.Id);

        Assert.Contains($"\"personId\":{personId}", (string)GetProperty(auditLog, "ChangedFieldsJson")!);
    }

    private async Task<long> SeedPersonWithoutAccountAsync()
    {
        long personId = Random.Shared.NextInt64(700000, 799999);

        using IServiceScope scope = factory.Services.CreateScope();
        MoeDbContext db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        db.Set<Person>().Add(new Person(
            personId,
            $"IT-OPEN-PERSON-{personId}",
            "Integration Open Account Student",
            new DateOnly(2008, 1, 1),
            "SG",
            "CITIZEN"));

        await db.SaveChangesAsync();
        return personId;
    }

    private static object SingleEntity(
        MoeDbContext db,
        string typeName,
        Func<object, bool> predicate)
    {
        Type entityType = typeof(Person).Assembly.GetType(typeName, throwOnError: true)!;
        IQueryable query = CreateQueryable(db, entityType);
        return query.Cast<object>().Single(predicate);
    }

    private static IQueryable CreateQueryable(MoeDbContext db, Type entityType)
    {
        MethodInfo setMethod = typeof(DbContext)
            .GetMethods()
            .Single(x => x.Name == nameof(DbContext.Set)
                && x.IsGenericMethod
                && x.GetParameters().Length == 0);

        return (IQueryable)setMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
    }

    private static object? GetProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName)!.GetValue(target);
    }
}
