using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.FasPayment.Domain.Fas;
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
        SeedFasData(db);

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

    private static void SeedFasData(MoeDbContext db)
    {
        var fasAssembly = typeof(Moe.Modules.FasPayment.Api.Admin.AdminFasSchemesController).Assembly;
        var schemeType = fasAssembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasScheme");
        var appType = fasAssembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasApplication");
        if (schemeType != null && appType != null)
        {
            var scheme = schemeType.GetMethod("CreateDraft")!.Invoke(null, new object[] { "FAS-E2E", "GRANT-E2E", "E2E Seeded Scheme", "Test", new DateOnly(2026,1,1), new DateOnly(2026,12,31), 1001L, DateTime.UtcNow });
            SetId(scheme!, 100);
            schemeType.GetMethod("Activate")!.Invoke(scheme, new object[] { 1001L, DateTime.UtcNow });
            db.Add(scheme!);

            var app1 = appType.GetMethod("Submit")!.Invoke(null, new object[] { "APP-001", 100L, "MOCKPASS-STUDENT-2001", "Tan Mei Ling", new DateOnly(2026, 6, 1) });
            SetId(app1!, 2001);
            db.Add(app1!);

            var app2 = appType.GetMethod("Submit")!.Invoke(null, new object[] { "APP-002", 100L, "MOCKPASS-STUDENT-2002", "Nur Aisyah", new DateOnly(2026, 6, 2) });
            SetId(app2!, 2002);
            db.Add(app2!);
        }
    }

    private static void SeedIdentityRows(MoeDbContext db)
    {
        DemoStudentSeed[] demoStudents =
        [
            new(2001, "MOCKPASS-STUDENT-2001", "Tan Mei Ling", new DateOnly(2008, 5, 12), "DEMO-STU-0001", "SEC_4", "4A", "ACTIVE", 2, "EA-DEMO-0001", 250.00m, false),
            new(2002, "MOCKPASS-STUDENT-2002", "Nur Aisyah", new DateOnly(2009, 3, 1), "DEMO-STU-0002", "SEC_3", "3B", "ACTIVE", 2, "EA-DEMO-0002", 80.00m, false),
            new(2003, "MOCKPASS-STUDENT-2003", "Loh Jun Jie", new DateOnly(2010, 9, 20), "DEMO-STU-0003", "SEC_2", "2C", "ACTIVE", 2, "EA-DEMO-0003", 600.00m, false),
            new(2004, "MOCKPASS-STUDENT-2004", "Alicia Tan", new DateOnly(2011, 11, 3), "DEMO-STU-0004", "SEC_1", "1A", "ACTIVE", 2, "EA-DEMO-0004", 15.50m, false),
            new(2005, "MOCKPASS-STUDENT-2005", "Mohamad Danish", new DateOnly(2012, 2, 14), "DEMO-STU-0005", "PRI_6", "6B", "ON_LEAVE", 2, "EA-DEMO-0005", 45.00m, false),
            new(2006, "MOCKPASS-STUDENT-2006", "Grace Ng", new DateOnly(2013, 7, 8), "DEMO-STU-0006", "PRI_5", "5C", "GRADUATED", 2, "EA-DEMO-0006", 0.00m, false),
            new(2007, "MOCKPASS-STUDENT-2007", "Ryan Lee", new DateOnly(2014, 1, 26), "DEMO-STU-0007", "PRI_4", "4D", "WITHDRAWN", 2, "EA-DEMO-0007", 130.25m, false),
            new(2008, "MOCKPASS-STUDENT-2008", "Farah Syazwani", new DateOnly(2015, 4, 18), "DEMO-STU-0008", "PRI_3", "3A", "ACTIVE", 3, "EA-DEMO-0008", 22.00m, false),
            new(2009, "MOCKPASS-STUDENT-2009", "Marcus Lim", new DateOnly(2000, 6, 30), "DEMO-STU-0009", "POST_SEC", "P1", "ACTIVE", 3, "EA-DEMO-0009", 500.00m, true),
            new(2010, "MOCKPASS-STUDENT-2010", "Sarah Chen", new DateOnly(2016, 10, 9), "DEMO-STU-0010", "PRI_2", "2B", "ON_LEAVE", 2, "EA-DEMO-0010", 9.75m, true),
        ];

        foreach (DemoStudentSeed student in demoStudents)
        {
            SeedStudentProfile(
                db,
                student.PersonId,
                student.ExternalSubjectId,
                student.FullName,
                student.DateOfBirth,
                student.StudentNumber,
                student.LevelCode,
                student.ClassCode,
                student.SchoolingStatusCode,
                student.OrganizationId);

            SeedDemoStudentAccount(
                db,
                student.PersonId,
                student.AccountNumber,
                student.Balance,
                student.CloseAccountAfterSeed);
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
                "MOE HQ Admin",
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

    }

    private static void SetId(object entity, long id)
    {
        entity.GetType().GetProperty("Id")!.SetValue(entity, id);
    }

    private static void SeedDemoStudentAccount(
        MoeDbContext db,
        long personId,
        string accountNumber,
        decimal balance,
        bool closeAccountAfterSeed = false)
    {
        if (db.Set<EducationAccount>().Any(x => x.PersonId == personId))
        {
            return;
        }

        var accountResult = EducationAccount.OpenManual(
            personId,
            accountNumber,
            DateTimeOffset.UtcNow,
            "E2E Seed",
            "Portal account seed",
            1001);

        if (!accountResult.IsSuccess)
        {
            return;
        }

        accountResult.Value.UpdateBalance(balance);
        if (closeAccountAfterSeed)
        {
            accountResult.Value.CloseManual(DateTimeOffset.UtcNow, EducationAccountClosingReasonCodes.Other, "Seeded closed account", 1001);
        }

        db.Set<EducationAccount>().Add(accountResult.Value);
    }

    private static void SeedStudentProfile(
        MoeDbContext db,
        long personId,
        string externalSubjectId,
        string fullName,
        DateOnly dateOfBirth,
        string studentNumber,
        string levelCode,
        string classCode,
        string schoolingStatusCode,
        long organizationId = 2)
    {
        if (!db.Set<Person>().Any(x => x.Id == personId))
        {
            db.Set<Person>().Add(new Person(
                personId,
                externalSubjectId,
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
        SetId(enrollment, 3000 + personId);
        SetProperty(enrollment, nameof(SchoolEnrollment.PersonId), personId);
        SetProperty(enrollment, nameof(SchoolEnrollment.OrganizationId), organizationId);
        SetProperty(enrollment, nameof(SchoolEnrollment.StudentNumber), studentNumber);
        SetProperty(enrollment, nameof(SchoolEnrollment.AcademicYear), "2026");
        SetProperty(enrollment, nameof(SchoolEnrollment.LevelCode), levelCode);
        SetProperty(enrollment, nameof(SchoolEnrollment.ClassCode), classCode);
        SetProperty(enrollment, nameof(SchoolEnrollment.SchoolingStatusCode), schoolingStatusCode);
        SetProperty(enrollment, nameof(SchoolEnrollment.StatusReasonCode), null);
        SetProperty(enrollment, nameof(SchoolEnrollment.StartDate), new DateOnly(2026, 1, 2));
        SetProperty(enrollment, nameof(SchoolEnrollment.EndDate), null);
        SetProperty(enrollment, nameof(SchoolEnrollment.SourceCode), "E2E_SEED");
        SetProperty(enrollment, nameof(SchoolEnrollment.CreatedAtUtc), DateTime.UtcNow);
        SetProperty(enrollment, nameof(SchoolEnrollment.UpdatedAtUtc), DateTime.UtcNow);
        db.Set<SchoolEnrollment>().Add(enrollment);
    }

    private static void SetProperty(object entity, string propertyName, object? value)
    {
        entity.GetType().GetProperty(propertyName)!.SetValue(entity, value);
    }

    private sealed record DemoStudentSeed(
        long PersonId,
        string ExternalSubjectId,
        string FullName,
        DateOnly DateOfBirth,
        string StudentNumber,
        string LevelCode,
        string ClassCode,
        string SchoolingStatusCode,
        long OrganizationId,
        string AccountNumber,
        decimal Balance,
        bool CloseAccountAfterSeed);
}
