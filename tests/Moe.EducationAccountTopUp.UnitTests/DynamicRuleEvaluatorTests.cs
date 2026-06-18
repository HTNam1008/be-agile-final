using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Modules.EducationAccountTopUp.Application.TopUps;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Moe.Application.Abstractions.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class DynamicRuleEvaluatorTests
{
    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<EducationAccount>().HasKey(x => x.Id);
            modelBuilder.Entity<SchoolEnrollment>().HasKey(x => x.Id);
        }
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new MoeDbContext(options, new[] { new TestModelConfigurationContributor() });
    }

    [Fact]
    public async Task ApplyRules_ShouldHandleLeapYear_WithoutCrashing()
    {
        using var dbContext = CreateDbContext();

        var person = new Person(1, "S1234567A", "Test", new DateOnly(2005, 1, 1), "SG", "C");
        dbContext.Set<Person>().Add(person);
        
        var account = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "Reason", "Remarks", 99).Value;
        dbContext.Set<EducationAccount>().Add(account);
        
        await dbContext.SaveChangesAsync();

        var rules = new List<TopUpCampaignRule>
        {
            TopUpCampaignRule.Create(1, "AGE", "GREATERTHAN", 18, null, null)
        };

        var nowUtc = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc); // Leap year

        var query = dbContext.Set<EducationAccount>().AsQueryable();
        
        var filteredQuery = DynamicRuleEvaluator.ApplyRules(dbContext, query, rules, nowUtc);

        var result = await filteredQuery.ToListAsync();

        // 2024 - 18 = 2006. 2005 is < 2006, so they are > 18.
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApplyRules_ShouldExcludeCorrectly_WithNotEquals()
    {
        using var dbContext = CreateDbContext();

        var account = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "Reason", "Remarks", 99).Value;
        account.UpdateBalance(500m);
        dbContext.Set<EducationAccount>().Add(account);
        
        await dbContext.SaveChangesAsync();

        var rules = new List<TopUpCampaignRule>
        {
            TopUpCampaignRule.Create(1, "ACCOUNTBALANCE", "NOTEQUALS", 500m, null, null)
        };

        var query = dbContext.Set<EducationAccount>().AsQueryable();
        var filteredQuery = DynamicRuleEvaluator.ApplyRules(dbContext, query, rules, DateTime.UtcNow);

        var result = await filteredQuery.ToListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ApplyRules_ShouldIncludeCorrectly_WithInJsonArray()
    {
        using var dbContext = CreateDbContext();

        var person = new Person(1, "S1234567A", "Test", new DateOnly(2010, 1, 1), "SG", "C");
        dbContext.Set<Person>().Add(person);
        
        var account = EducationAccount.OpenManual(1, "EA-001", DateTimeOffset.UtcNow, "Reason", "Remarks", 99).Value;
        dbContext.Set<EducationAccount>().Add(account);

        var enrollment = (SchoolEnrollment)Activator.CreateInstance(typeof(SchoolEnrollment), true)!;
        typeof(SchoolEnrollment).GetProperty("PersonId")!.SetValue(enrollment, 1L);
        typeof(SchoolEnrollment).GetProperty("LevelCode")!.SetValue(enrollment, "Primary");
        typeof(SchoolEnrollment).GetProperty("SchoolingStatusCode")!.SetValue(enrollment, "Active");
        typeof(SchoolEnrollment).GetProperty("StartDate")!.SetValue(enrollment, new DateOnly(2015, 1, 1));
        dbContext.Set<SchoolEnrollment>().Add(enrollment);

        await dbContext.SaveChangesAsync();

        var rules = new List<TopUpCampaignRule>
        {
            TopUpCampaignRule.Create(1, "LEVEL", "IN", null, null, "[\"Primary\", \"Secondary\"]")
        };

        var query = dbContext.Set<EducationAccount>().AsQueryable();
        var filteredQuery = DynamicRuleEvaluator.ApplyRules(dbContext, query, rules, DateTime.UtcNow);

        var result = await filteredQuery.ToListAsync();

        result.Should().HaveCount(1);
    }
}
