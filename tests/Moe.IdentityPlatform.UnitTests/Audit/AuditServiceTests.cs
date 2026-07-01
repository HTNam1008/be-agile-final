using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Application.Abstractions.Security;
using Moe.Modules.IdentityPlatform.Domain.Audit;
using Moe.Modules.IdentityPlatform.Infrastructure.Audit;
using Moe.Modules.IdentityPlatform.Infrastructure.Persistence;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Audit;

public sealed class AuditServiceTests
{
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 6, 22, 4, 30, 0, TimeSpan.Zero));
    private readonly FakeCurrentUser _currentUser = new()
    {
        UserAccountId = 42,
        PersonId = 5001,
        OrganizationUnitId = 7,
        Portal = "AdminPortal",
        IsAuthenticated = true
    };

    [Fact]
    public async Task RecordAsync_AddsAuditLogToChangeTracker_DoesNotSaveImmediately()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = CreateService(dbContext);

        await service.RecordAsync(
            "EDUCATION_ACCOUNT_CLOSED_MANUALLY",
            "EducationAccount",
            "12345",
            """{"reasonCode":"GRADUATED"}""");

        dbContext.ChangeTracker.Entries<AuditLog>().Should().ContainSingle();
        dbContext.ChangeTracker.Entries<AuditLog>().Single().State.Should().Be(EntityState.Added);
        dbContext.Set<AuditLog>().Local.Should().ContainSingle();
        dbContext.Set<AuditLog>().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_PopulatesActorFromCurrentUser()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = CreateService(dbContext);

        await service.RecordAsync("ACCOUNT_DETAILS_UPDATED_BY_ADMIN", "EducationAccount", "12345");

        AuditLog auditLog = dbContext.Set<AuditLog>().Local.Single();
        auditLog.ActorLoginAccountId.Should().Be(42);
        auditLog.PersonId.Should().Be(5001);
        auditLog.OrganizationId.Should().Be(7);
        auditLog.ActorTypeCode.Should().Be("ADMIN");
    }

    [Fact]
    public async Task RecordAsync_PopulatesTimestampFromClock()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = CreateService(dbContext);

        await service.RecordAsync("EDUCATION_ACCOUNT_CREATED_MANUALLY", "EducationAccount", "12345");

        AuditLog auditLog = dbContext.Set<AuditLog>().Local.Single();
        auditLog.OccurredAtUtc.Should().Be(_clock.UtcNow.UtcDateTime);
    }

    [Fact]
    public async Task RecordAsync_WhenUnauthenticatedWithoutActor_UsesSystemActor()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = new(dbContext, new FakeCurrentUser(), _clock);

        await service.RecordAsync("EDUCATION_ACCOUNT_CREATED_AUTOMATICALLY", "EducationAccount", "12345");

        AuditLog auditLog = dbContext.Set<AuditLog>().Local.Single();
        auditLog.AuditScopeCode.Should().Be("SYSTEM");
        auditLog.ActorTypeCode.Should().Be("SYSTEM");
        auditLog.ActorLoginAccountId.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_WithNullDetailsJson_DoesNotThrow()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = CreateService(dbContext);

        Func<Task> act = () => service.RecordAsync(
            "EDUCATION_ACCOUNT_CREATED_MANUALLY",
            "EducationAccount",
            "12345",
            detailsJson: null);

        await act.Should().NotThrowAsync();
        dbContext.Set<AuditLog>().Local.Single().ChangedFieldsJson.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_PropagatesException_WhenAddFails()
    {
        using MoeDbContext dbContext = CreateDbContext();
        AuditService service = CreateService(dbContext);

        Func<Task> act = () => service.RecordAsync(
            "EDUCATION_ACCOUNT_CREATED_MANUALLY",
            "EducationAccount",
            "not-a-long");

        await act.Should().ThrowAsync<FormatException>();
        dbContext.ChangeTracker.Entries<AuditLog>().Should().BeEmpty();
    }

    private AuditService CreateService(MoeDbContext dbContext)
        => new(dbContext, _currentUser, _clock);

    private static MoeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MoeDbContext(options, new[] { new TestModelConfigurationContributor() });
    }

    private sealed class TestModelConfigurationContributor : IModelConfigurationContributor
    {
        public void Configure(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? UserAccountId { get; init; }
        public long? PersonId { get; init; }
        public long? OrganizationUnitId { get; init; }
        public IReadOnlyCollection<long> OrganizationUnitIds { get; init; } = Array.Empty<long>();
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Permissions { get; init; } = Array.Empty<string>();
        public string Portal { get; init; } = string.Empty;
        public bool IsAuthenticated { get; init; }
        public bool HasPermission(string permission) => Permissions.Contains(permission);
    }
}
