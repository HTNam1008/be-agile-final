using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
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
        SeedDemoBillingData(db);

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
            var scheme = schemeType.GetMethod("CreateDraft")!.Invoke(null, new object[] { "FAS-E2E", "GRANT-E2E", "E2E Seeded Scheme", "Test", DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), 1001L, DateTime.UtcNow });
            SetId(scheme!, 100);
            schemeType.GetMethod("Activate")!.Invoke(scheme, new object[] { 1001L, DateTime.UtcNow });
            db.Add(scheme!);

            Type tierType = fasAssembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasTier", throwOnError: true)!;
            Type criteriaType = fasAssembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasTierCriteria", throwOnError: true)!;
            Type nationalityType = fasAssembly.GetType("Moe.Modules.FasPayment.Domain.Fas.FasTierCriteriaNationality", throwOnError: true)!;

            object tier = tierType.GetMethod("Create")!.Invoke(null, [100L, "Demo Full Support", "PERCENTAGE", 100m, 1, DateTime.UtcNow])!;
            SetId(tier, 1001);
            db.Add(tier);

            SeedFasCriterion(db, criteriaType, nationalityType, 1101, 1001, "AGE", 13m, 25m, "AND", 1);
            SeedFasCriterion(db, criteriaType, nationalityType, 1102, 1001, "PCI", 0m, 1000m, "AND", 2);
            SeedFasCriterion(db, criteriaType, nationalityType, 1103, 1001, "PARENT_NATIONALITY", null, null, "AND", 3, "Singapore Citizen");
            SeedFasCriterion(db, criteriaType, nationalityType, 1104, 1001, "ACCOUNT_TYPE", null, null, null, 4, "EDUCATION_ACCOUNT");

            var app1 = appType.GetMethod("Submit")!.Invoke(null, new object[] { "APP-001", 100L, "ef39a074-b64d-4990-a937-6f80772e2bb8", "Tan Mei Ling", new DateOnly(2026, 6, 1) });
            SetId(app1!, 2001);
            db.Add(app1!);

            var app2 = appType.GetMethod("Submit")!.Invoke(null, new object[] { "APP-002", 100L, "MOCKPASS-STUDENT-2002", "Nur Aisyah", new DateOnly(2026, 6, 2) });
            SetId(app2!, 2002);
            db.Add(app2!);
        }
    }

    private static void SeedFasCriterion(
        MoeDbContext db,
        Type criteriaType,
        Type nationalityType,
        long criteriaId,
        long tierId,
        string criteria,
        decimal? numberFrom,
        decimal? numberTo,
        string? connector,
        int displayOrder,
        string? categoricalValue = null)
    {
        object row = criteriaType.GetMethod("Create")!.Invoke(
            null,
            [tierId, criteria, numberFrom, numberTo, connector, displayOrder, DateTime.UtcNow, criteriaId])!;
        db.Add(row);

        if (!string.IsNullOrWhiteSpace(categoricalValue))
        {
            object category = nationalityType.GetMethod("Create")!.Invoke(null, [criteriaId, criteria, categoricalValue])!;
            db.Add(category);
        }
    }

    private static void SeedDemoBillingData(MoeDbContext db)
    {
        Assembly courseAssembly = typeof(Moe.Modules.CourseBilling.CourseBillingModule).Assembly;
        Type courseType = courseAssembly.GetType("Moe.Modules.CourseBilling.Domain.Courses.Course", throwOnError: true)!;
        Type enrollmentType = courseAssembly.GetType("Moe.Modules.CourseBilling.Domain.Courses.CourseEnrollment", throwOnError: true)!;
        Type billType = courseAssembly.GetType("Moe.Modules.CourseBilling.Domain.Billing.Bill", throwOnError: true)!;
        Type billLineType = courseAssembly.GetType("Moe.Modules.CourseBilling.Domain.Billing.BillLine", throwOnError: true)!;
        Assembly paymentAssembly = typeof(Moe.Modules.FasPayment.Api.EService.EServicePaymentsController).Assembly;
        Type paymentType = paymentAssembly.GetType("Moe.Modules.FasPayment.Domain.Payments.Payment", throwOnError: true)!;
        Type paymentRefundType = paymentAssembly.GetType("Moe.Modules.FasPayment.Domain.Payments.PaymentRefund", throwOnError: true)!;

        if (db.Find(courseType, 9001L) is null)
        {
            ConstructorInfo courseCtor = courseType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(ctor => ctor.GetParameters().Length == 12);
            object course = courseCtor.Invoke([
                2L,
                "DEMO-AI-001",
                "Client Demo Robotics Programme",
                "Seeded course for AI Copilot payment demonstration.",
                new DateOnly(2026, 7, 1),
                new DateOnly(2026, 9, 30),
                DateTime.UtcNow.AddDays(-30),
                DateTime.UtcNow.AddDays(30),
                1001L,
                DateTime.UtcNow,
                100m,
                50m
            ]);
            SetId(course, 9001);
            courseType.GetMethod("Publish")!.Invoke(course, [1001L, DateTime.UtcNow]);
            db.Add(course);
        }

        if (db.Find(enrollmentType, 9002L) is null)
        {
            object enrollmentResult = enrollmentType.GetMethod("EnrollByAdmin")!.Invoke(
                null,
                [2001L, 9001L, 9101L, 1001L, DateTime.UtcNow.AddDays(-7), 100m, 50m])!;
            object enrollment = UnwrapResult(enrollmentResult);
            SetId(enrollment, 9002);
            db.Add(enrollment);
        }

        if (db.Find(billType, 9003L) is null)
        {
            object billResult = billType.GetMethod("IssueForCourseEnrollment")!.Invoke(
                null,
                [9002L, "BILL-DEMO-001", DateTime.UtcNow.AddDays(-5), new DateOnly(2026, 7, 15), 420m, 120m, 1])!;
            object bill = UnwrapResult(billResult);
            SetId(bill, 9003);
            db.Add(bill);
        }

        if (db.Find(billLineType, 9004L) is null)
        {
            object lineResult = billLineType.GetMethod("FromCourseFee")!.Invoke(
                null,
                [9003L, 9201L, 9301L, "Robotics programme fee after subsidy", 300m, 0m])!;
            object line = UnwrapResult(lineResult);
            SetId(line, 9004);
            db.Add(line);
        }

        if (db.Find(paymentType, 9005L) is null)
        {
            object payment = paymentType.GetMethod("RecordProviderSuccess")!.Invoke(
                null,
                [9003L, 2001L, 120m, "pi_demo_ai_copilot", "in_demo_ai_copilot", "ch_demo_ai_copilot", 1, DateTime.UtcNow.AddDays(-14)])!;
            SetId(payment, 9005);
            paymentType.GetMethod("ApplyProviderRefundTotal")!.Invoke(payment, [40m]);
            db.Add(payment);
        }

        if (db.Find(paymentRefundType, 9006L) is null)
        {
            object refundResult = paymentRefundType.GetMethod("Create")!.Invoke(
                null,
                [9005L, 40m, "Demo partial refund", 1001L, DateTime.UtcNow.AddDays(-10)])!;
            object refund = UnwrapResult(refundResult);
            SetId(refund, 9006);
            paymentRefundType.GetMethod("AssignProviderRefund")!.Invoke(refund, ["re_demo_ai_copilot"]);
            paymentRefundType.GetMethod("MarkSucceeded")!.Invoke(refund, [DateTime.UtcNow.AddDays(-9)]);
            db.Add(refund);
        }
    }

    private static void SeedIdentityRows(MoeDbContext db)
    {
        DemoStudentSeed[] demoStudents =
        [
            new(2001, "ef39a074-b64d-4990-a937-6f80772e2bb8", "Tan Mei Ling", new DateOnly(2008, 5, 12), "DEMO-STU-0001", "POST_SEC", "4A", "ACTIVE", 2, "EA-DEMO-0001", 250.00m, false),
            new(2002, "MOCKPASS-STUDENT-2002", "Nur Aisyah", new DateOnly(2009, 3, 1), "DEMO-STU-0002", "POST_SEC", "3B", "ACTIVE", 2, "EA-DEMO-0002", 80.00m, false),
            new(2003, "MOCKPASS-STUDENT-2003", "Loh Jun Jie", new DateOnly(2010, 9, 20), "DEMO-STU-0003", "POST_SEC", "2C", "ACTIVE", 2, "EA-DEMO-0003", 600.00m, false),
            new(2004, "MOCKPASS-STUDENT-2004", "Alicia Tan", new DateOnly(2011, 11, 3), "DEMO-STU-0004", "POST_SEC", "1A", "ACTIVE", 2, "EA-DEMO-0004", 15.50m, false),
            new(2005, "MOCKPASS-STUDENT-2005", "Mohamad Danish", new DateOnly(2012, 2, 14), "DEMO-STU-0005", "POST_SEC", "6B", "ON_LEAVE", 2, "EA-DEMO-0005", 45.00m, false),
            new(2006, "MOCKPASS-STUDENT-2006", "Grace Ng", new DateOnly(2013, 7, 8), "DEMO-STU-0006", "POST_SEC", "5C", "GRADUATED", 2, "EA-DEMO-0006", 0.00m, false),
            new(2007, "MOCKPASS-STUDENT-2007", "Ryan Lee", new DateOnly(2014, 1, 26), "DEMO-STU-0007", "POST_SEC", "4D", "WITHDRAWN", 2, "EA-DEMO-0007", 130.25m, false),
            new(2008, "MOCKPASS-STUDENT-2008", "Farah Syazwani", new DateOnly(2015, 4, 18), "DEMO-STU-0008", "POST_SEC", "3A", "ACTIVE", 3, "EA-DEMO-0008", 22.00m, false),
            new(2009, "MOCKPASS-STUDENT-2009", "Marcus Lim", new DateOnly(2000, 6, 30), "DEMO-STU-0009", "POST_SEC", "P1", "ACTIVE", 3, "EA-DEMO-0009", 500.00m, true),
            new(2010, "MOCKPASS-STUDENT-2010", "Sarah Chen", new DateOnly(2016, 10, 9), "DEMO-STU-0010", "POST_SEC", "2B", "ON_LEAVE", 2, "EA-DEMO-0010", 9.75m, true),
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
                "http://localhost:5156/singpass/v3/fapi",
                "ef39a074-b64d-4990-a937-6f80772e2bb8",
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

    private static object UnwrapResult(object result)
    {
        bool success = (bool)result.GetType().GetProperty("IsSuccess")!.GetValue(result)!;
        if (!success)
        {
            throw new InvalidOperationException("E2E seed domain factory rejected the demo row.");
        }

        return result.GetType().GetProperty("Value")!.GetValue(result)!;
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
