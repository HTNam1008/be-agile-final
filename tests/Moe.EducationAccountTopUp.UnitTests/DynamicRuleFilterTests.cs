using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Contracts.TopUps.Enums;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.EducationAccountTopUp.IGateway.TopUps;
using Moe.Modules.EducationAccountTopUp.Infrastructure.TopUps;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.EducationAccountTopUp.UnitTests;

public sealed class DynamicRuleFilterTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FilterAccountIdsAsync_Should_OrAcrossGroups()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        EducationAccount lowBalance = await AddAccountAsync(dbContext, 1, 20m);
        EducationAccount middleBalance = await AddAccountAsync(dbContext, 2, 250m);
        EducationAccount highBalance = await AddAccountAsync(dbContext, 3, 900m);
        var filter = new DynamicRuleFilter(dbContext);

        IReadOnlyList<long> result = await filter.FilterAccountIdsAsync(
            [
                Group(1, Rule(1, 1, TopUpCriterionCode.AccountBalance, OperatorCode.Between, 0m, 100m)),
                Group(2, Rule(2, 2, TopUpCriterionCode.AccountBalance, OperatorCode.Between, 800m, 1000m))
            ],
            skip: 0,
            take: 10,
            nowUtc: NowUtc);

        result.Should().Equal(lowBalance.Id, highBalance.Id);
        result.Should().NotContain(middleBalance.Id);
    }

    [Fact]
    public async Task FilterAccountIdsAsync_Should_AndWithinGroup()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        EducationAccount matching = await AddAccountAsync(dbContext, 1, 80m, levelCode: "BACHELOR");
        EducationAccount wrongLevel = await AddAccountAsync(dbContext, 2, 80m, levelCode: "MASTER");
        EducationAccount wrongBalance = await AddAccountAsync(dbContext, 3, 250m, levelCode: "BACHELOR");
        var filter = new DynamicRuleFilter(dbContext);

        IReadOnlyList<long> result = await filter.FilterAccountIdsAsync(
            [
                Group(1,
                    Rule(1, 1, TopUpCriterionCode.AccountBalance, OperatorCode.Between, 0m, 100m),
                    Rule(2, 1, TopUpCriterionCode.Level, OperatorCode.Equals, textValue: "BACHELOR"))
            ],
            skip: 0,
            take: 10,
            nowUtc: NowUtc);

        result.Should().Equal(matching.Id);
        result.Should().NotContain([wrongLevel.Id, wrongBalance.Id]);
    }

    [Fact]
    public async Task CountMatchingAccountsAsync_Should_ReturnZero_When_HasEducationAccountNo()
    {
        await using MoeDbContext dbContext = CreateDbContext();
        await AddAccountAsync(dbContext, 1, 20m);
        var filter = new DynamicRuleFilter(dbContext);

        int count = await filter.CountMatchingAccountsAsync(
            [
                Group(1, Rule(1, 1, TopUpCriterionCode.HasEducationAccount, OperatorCode.Equals, textValue: "NO"))
            ],
            NowUtc);

        count.Should().Be(0);
    }

    private static CampaignRuleGroupProjection Group(
        long groupId,
        params CampaignRuleProjection[] rules)
        => new(groupId, (int)groupId, rules);

    private static CampaignRuleProjection Rule(
        long id,
        long groupId,
        TopUpCriterionCode criterion,
        OperatorCode op,
        decimal? from = null,
        decimal? to = null,
        string? textValue = null)
        => new(id, groupId, (int)id, criterion.ToString(), op.ToString(), from, to, textValue);

    private static async Task<EducationAccount> AddAccountAsync(
        MoeDbContext dbContext,
        long personId,
        decimal balance,
        string levelCode = "BACHELOR")
    {
        var account = EducationAccount.OpenManual(
            personId,
            $"EA-{personId:000}",
            new DateTimeOffset(NowUtc),
            "TEST",
            "Test account",
            openedBy: 1).Value;
        account.UpdateBalance(balance);

        dbContext.Set<Person>().Add(new Person(
            personId,
            $"EXT-{personId}",
            $"Student {personId}",
            new DateOnly(2004, 1, 1),
            "SG",
            null));

        dbContext.Set<SchoolEnrollment>().Add(new SchoolEnrollment(
            personId,
            organizationId: 1,
            studentNumber: $"S{personId:000}",
            academicYear: "2026",
            levelCode,
            classCode: "A1",
            startDate: new DateOnly(2026, 1, 1),
            utcNow: NowUtc));

        dbContext.Set<EducationAccount>().Add(account);
        await dbContext.SaveChangesAsync();
        return account;
    }

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, [new TestModelConfigurationContributor()]);
    }

    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>().HasKey(x => x.Id);
            modelBuilder.Entity<EducationAccount>().HasKey(x => x.Id);
            modelBuilder.Entity<SchoolEnrollment>().HasKey(x => x.Id);
        }
    }
}
