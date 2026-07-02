using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.FasPayment.Contracts.AdminFasSchemes;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.IGateway.Repositories;
using Moe.Modules.FasPayment.Infrastructure.Repositories;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.FasPayment.SqlTests;

public sealed class FasSqlPersistenceTests
{
    private const string PreviousMigration = "20260620152508_SyncMockPassDemoSeed";

    [Fact]
    public async Task Clean_migration_contains_v4_keys_constraints_and_indexes()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync();
        await using MoeDbContext context = database.CreateContext();

        int tables = await Scalar(context, "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID('fas') AND name IN ('FASScheme','FASSchemeCourse','FASTier','FASTierCriteria','FASTierCriteriaNationality','FASApplicationReviewDecision')");
        int checks = await Scalar(context, "SELECT COUNT(*) AS [Value] FROM sys.check_constraints WHERE name LIKE 'CK_FAS%'");
        int uniqueIndexes = await Scalar(context, "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE is_unique = 1 AND name IN ('IX_FASScheme_SchemeCode','IX_FASScheme_GrantCode','IX_FASTier_FASSchemeId_DisplayOrder','IX_FASTierCriteria_FASTierId_DisplayOrder')");

        tables.Should().Be(6);
        checks.Should().BeGreaterThanOrEqualTo(8);
        uniqueIndexes.Should().Be(4);
    }

    [Fact]
    public async Task Legacy_upgrade_backfills_scheme_and_tier_values()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync(PreviousMigration);
        await using (MoeDbContext legacy = database.CreateContext())
        {
            await legacy.Database.ExecuteSqlRawAsync("INSERT INTO [fas].[FASScheme] ([SchemeCode],[SchemeName],[ProviderName],[EffectiveFrom],[EffectiveTo],[SchemeStatusCode]) VALUES ('LEGACY-1','Legacy Scheme','MOE','2026-01-01',NULL,'PUBLISHED')");
            await legacy.Database.ExecuteSqlRawAsync("DECLARE @id bigint=(SELECT [FASSchemeId] FROM [fas].[FASScheme] WHERE [SchemeCode]='LEGACY-1'); INSERT INTO [fas].[FASTier] ([FASSchemeId],[TierCode],[TierName],[PriorityNumber],[StatusCode]) VALUES (@id,'T1','Legacy Tier',1,'ACTIVE')");
            await legacy.Database.MigrateAsync();
        }
        await using MoeDbContext upgraded = database.CreateContext();
        string scheme = await upgraded.Database.SqlQueryRaw<string>("SELECT CONCAT([Name],'|',[GrantCode],'|',[StatusCode],'|',CONVERT(varchar(10),[EndDate],23)) AS [Value] FROM [fas].[FASScheme] WHERE [SchemeCode]='LEGACY-1'").SingleAsync();
        string tier = await upgraded.Database.SqlQueryRaw<string>("SELECT CONCAT([Label],'|',[SubsidyType],'|',[SubsidyValue]) AS [Value] FROM [fas].[FASTier]").SingleAsync();
        scheme.Should().Be("Legacy Scheme|LEGACY-1|ACTIVE|2026-01-01");
        tier.Should().Be("Legacy Tier|FIXED|0.00");
    }

    [Fact]
    public async Task Course_bridge_and_global_semantics_are_persisted_exactly()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync();
        await using MoeDbContext context = database.CreateContext();
        long courseId = await SeedCourse(context);
        FasSchemeRepository repository = Repository(context);

        CreateFasSchemeResponse global = await repository.CreateAsync(Request("GLOBAL", []), 77, DateTime.UtcNow, CancellationToken.None);
        CreateFasSchemeResponse restricted = await repository.CreateAsync(Request("RESTRICTED", [courseId]), 77, DateTime.UtcNow, CancellationToken.None);

        (await context.Set<FasSchemeCourse>().CountAsync(x => x.FasSchemeId == global.SchemeId)).Should().Be(0);
        (await context.Set<FasSchemeCourse>().SingleAsync(x => x.FasSchemeId == restricted.SchemeId)).CourseId.Should().Be(courseId);
        Func<Task> restrictedCourseDelete = () => context.Database.ExecuteSqlAsync($"DELETE FROM [course].[Course] WHERE [CourseId]={courseId}");
        await restrictedCourseDelete.Should().ThrowAsync<SqlException>();
        context.Remove(await context.Set<FasScheme>().SingleAsync(x => x.Id == restricted.SchemeId));
        await context.SaveChangesAsync();
        (await context.Set<FasSchemeCourse>().AnyAsync(x => x.FasSchemeId == restricted.SchemeId)).Should().BeFalse();
    }

    [Fact]
    public async Task Child_insert_failure_rolls_back_the_whole_aggregate()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync();
        await using MoeDbContext context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER [fas].[TR_FASTierCriteria_AoeFailure] ON [fas].[FASTierCriteria] INSTEAD OF INSERT AS THROW 51000, 'AOE forced child failure', 1");
        FasSchemeRepository repository = Repository(context);

        Func<Task> action = () => repository.CreateAsync(Request("ROLLBACK", []), 77, DateTime.UtcNow, CancellationToken.None);

        await action.Should().ThrowAsync<DbUpdateException>();
        context.ChangeTracker.Clear();
        (await context.Set<FasScheme>().AnyAsync(x => x.SchemeCode == "SCHEME-ROLLBACK")).Should().BeFalse();
        (await context.Set<FasTier>().AnyAsync()).Should().BeFalse();
        (await context.Set<FasTierCriteria>().AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_duplicate_repository_writes_yield_one_success_and_one_controlled_conflict()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync();
        await using MoeDbContext firstContext = database.CreateContext();
        await using MoeDbContext secondContext = database.CreateContext();
        Task<CreateFasSchemeResponse> first = Repository(firstContext).CreateAsync(Request("RACE", []), 77, DateTime.UtcNow, CancellationToken.None);
        Task<CreateFasSchemeResponse> second = Repository(secondContext).CreateAsync(Request("RACE", []), 78, DateTime.UtcNow, CancellationToken.None);

        await Task.WhenAll(first.ContinueWith(_ => { }), second.ContinueWith(_ => { }));

        new[] { first, second }.Count(task => task.Status == TaskStatus.RanToCompletion).Should().Be(1);
        Task<CreateFasSchemeResponse> failed = new[] { first, second }.Single(task => task.IsFaulted);
        failed.Exception!.GetBaseException().Should().BeOfType<FasSchemeWriteConflictException>();
        await using MoeDbContext verification = database.CreateContext();
        (await verification.Set<FasScheme>().CountAsync(x => x.SchemeCode == "SCHEME-RACE")).Should().Be(1);
    }

    [Fact]
    public async Task Inconsistent_duplicated_detail_data_is_logged_and_rejected()
    {
        if (!SqlFasTestDatabase.IsEnabled) return;
        await using SqlFasTestDatabase database = await SqlFasTestDatabase.CreateAsync();
        await using MoeDbContext context = database.CreateContext();
        var logger = new RecordingLogger<FasSchemeRepository>();
        var repository = new FasSchemeRepository(context, logger, new FixedClock(DateTimeOffset.UtcNow));
        CreateFasSchemeRequest request = Request("CORRUPT", []) with
        {
            Tiers =
            [
                new("First", 100, 1, [new(1, 13, 18, null)]),
                new("Second", 50, 2, [new(1, 13, 18, null)])
            ]
        };
        CreateFasSchemeResponse created = await repository.CreateAsync(request, 77, DateTime.UtcNow, CancellationToken.None);
        await context.Database.ExecuteSqlAsync($"UPDATE [fas].[FASTier] SET [SubsidyType]='FIXED' WHERE [FASSchemeId]={created.SchemeId} AND [DisplayOrder]=2");

        Func<Task> action = async () => await repository.GetAsync(created.SchemeId, CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inconsistent data*");
        logger.Errors.Should().Contain(message => message.Contains(created.SchemeId.ToString(), StringComparison.Ordinal));
    }

    private static FasSchemeRepository Repository(MoeDbContext context)
        => new(context, NullLogger<FasSchemeRepository>.Instance, new FixedClock(DateTimeOffset.UtcNow));

    private static CreateFasSchemeRequest Request(string suffix, IReadOnlyList<long> courseIds) => new(
        $"SCHEME-{suffix}", $"GRANT-{suffix}", $"Scheme {suffix}", null,
        new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), courseIds, "PERCENTAGE",
        [new("AGE", null, 1)],
        [new("Full", 100, 1, [new(1, 13, 18, null)])]);

    private static async Task<long> SeedCourse(MoeDbContext context)
    {
        List<long> ids = await context.Database.SqlQueryRaw<long>("INSERT INTO [course].[Course] ([OrganizationId],[CourseCode],[CourseName],[StartDate],[EndDate],[EnrollmentOpenAt],[EnrollmentCloseAt],[CourseStatusCode],[CreatedByLoginAccountId],[UpdatedByLoginAccountId],[UpdatedAt]) OUTPUT INSERTED.[CourseId] AS [Value] VALUES (1,'AOE-C','AOE Course','2026-01-01','2026-12-31',SYSUTCDATETIME(),'2026-12-01','DRAFT',1,1,SYSUTCDATETIME())").ToListAsync();
        return ids.Single();
    }

    private static Task<int> Scalar(MoeDbContext context, string sql)
        => context.Database.SqlQueryRaw<int>(sql).SingleAsync();
}

file sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;

    public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Errors { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Error) Errors.Add(formatter(state, exception));
    }
}
