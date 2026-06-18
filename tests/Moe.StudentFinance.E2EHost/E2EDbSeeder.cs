using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.StudentFinance.Persistence;

namespace Moe.StudentFinance.E2EHost;

public class E2EDbSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public E2EDbSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MoeDbContext>();

        SeedIdentityRows(db);

        // We use Reflection or direct EF Core insertions to bypass domain rules if necessary, 
        // but OpenManual is better. However, EducationAccount uses long Id. 
        // In EF Core InMemory, if we just Add, it assigns an Id. Let's explicitly set properties via reflection to bypass if needed, or just let EF Core do it.
        
        var acc1Result = EducationAccount.OpenManual(100, "ACC-1001", DateTimeOffset.UtcNow, "E2E Seed", "Seed", 1);
        if (acc1Result.IsSuccess)
        {
            var acc1 = acc1Result.Value;
            acc1.UpdateBalance(20.00m); // Balance < 50 for LESS_THAN dynamic rule
            db.Set<EducationAccount>().Add(acc1);
        }

        var acc2Result = EducationAccount.OpenManual(101, "ACC-1002", DateTimeOffset.UtcNow, "E2E Seed", "Seed", 1);
        if (acc2Result.IsSuccess)
        {
            var acc2 = acc2Result.Value;
            acc2.UpdateBalance(100.00m); // Balance > 50 
            db.Set<EducationAccount>().Add(acc2);
        }

        var acc3Result = EducationAccount.OpenManual(102, "ACC-1003", DateTimeOffset.UtcNow, "E2E Seed", "Seed", 1);
        if (acc3Result.IsSuccess)
        {
            var acc3 = acc3Result.Value;
            acc3.UpdateBalance(1000.00m); // Balance > 500 for GREATER_THAN dynamic rule
            db.Set<EducationAccount>().Add(acc3);
        }

        await db.SaveChangesAsync(cancellationToken);

        // We also need to map EducationAccount ID 1 and 2 because we hardcoded them in FixedSelection test?
        // Wait, EF Core InMemory will auto-assign IDs 1 and 2 for these two accounts!
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void SeedIdentityRows(MoeDbContext db)
    {
        if (!db.Set<Person>().Any(x => x.Id == 2001))
        {
            db.Set<Person>().Add(new Person(
                2001,
                "MOCKPASS-STUDENT-2001",
                "Tan Mei Ling",
                new DateOnly(2008, 5, 12),
                "SG",
                "CITIZEN"));
        }

        Type userAccountType = typeof(Person).Assembly.GetType(
            "Moe.Modules.IdentityPlatform.Domain.Iam.UserAccount",
            throwOnError: true)!;

        if (db.Find(userAccountType, 1001L) is null)
        {
            object admin = userAccountType.GetMethod("CreateBootstrapAdmin")!.Invoke(null,
            [
                "https://sts.windows.net/e2e/",
                "e2e-admin-object-id",
                "e2e-tenant-id",
                "e2e-admin-object-id",
                "system.admin@moe.local",
                "MOE System Admin",
                DateTime.UtcNow
            ])!;
            SetId(admin, 1001);
            db.Add(admin);
        }

        if (db.Find(userAccountType, 1003L) is null)
        {
            object student = userAccountType.GetMethod("CreateStudentSingpass")!.Invoke(null,
            [
                2001L,
                "http://localhost:5001/mockpass",
                "MOCKPASS-STUDENT-2001",
                "Tan Mei Ling",
                1001L,
                DateTime.UtcNow
            ])!;
            SetId(student, 1003);
            db.Add(student);
        }

        if (!db.Set<EducationAccount>().Any(x => x.PersonId == 2001))
        {
            var accountResult = EducationAccount.OpenManual(
                2001,
                "EA-DEMO-0001",
                DateTimeOffset.UtcNow,
                "E2E Seed",
                "Portal account seed",
                1001);

            if (accountResult.IsSuccess)
            {
                accountResult.Value.UpdateBalance(250.00m);
                db.Set<EducationAccount>().Add(accountResult.Value);
            }
        }
    }

    private static void SetId(object entity, long id)
    {
        entity.GetType().GetProperty("Id")!.SetValue(entity, id);
    }
}
