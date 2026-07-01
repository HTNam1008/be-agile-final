using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Clock;
using Moe.Modules.IdentityPlatform;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.Infrastructure.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.IdentityPlatform.IGateway.Students.TopUpSearch;
using Moe.StudentFinance.Persistence;
using Xunit;

namespace Moe.IdentityPlatform.UnitTests.Infrastructure.Students;

public sealed class SingaporeBusinessDayRedTests
{
    private static readonly DateTimeOffset SgtEarlyMorning =
        new(2026, 6, 30, 16, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TopUpStudentSearchDirectory_uses_singapore_business_day_for_age_cutoff()
    {
        await using MoeDbContext db = CreateDbContext();
        db.Add(new Person(1, "P1", "Turns 18 Today", new DateOnly(2008, 7, 1), "SG", "CITIZEN"));
        db.Add(new SchoolEnrollment(1, 10, "S001", "2026", "SEC4", null, new DateOnly(2026, 1, 1), SgtEarlyMorning.UtcDateTime));
        await db.SaveChangesAsync();
        TopUpStudentSearchDirectory directory = new(db, new TestClock(SgtEarlyMorning));

        TopUpStudentSearchSummaryPage page = await directory.SearchForTopUpAsync(
            new TopUpStudentSearchCriteria(null, null, null, 10, "ACTIVE", null, null, 18, null, 1, 20),
            [10],
            CancellationToken.None);

        page.Items.Should().ContainSingle(x => x.PersonId == 1);
    }

    [Fact]
    public async Task StudentDirectory_uses_singapore_business_day_for_active_enrollment_window()
    {
        await using MoeDbContext db = CreateDbContext();
        db.Add(new Person(2, "P2", "Starts Today", new DateOnly(2008, 1, 1), "SG", "CITIZEN"));
        db.Add(new SchoolEnrollment(2, 10, "S002", "2026", "SEC4", null, new DateOnly(2026, 7, 1), SgtEarlyMorning.UtcDateTime));
        await db.SaveChangesAsync();
        StudentDirectory directory = new(db, new TestClock(SgtEarlyMorning));

        IReadOnlyList<AdminStudentSearchSummary> students = await directory.ListByOrganizationAsync(
            new AdminStudentSearchCriteria(10, null, null, null, 1, 20),
            CancellationToken.None);

        students.Should().ContainSingle(x => x.PersonId == 2);
    }

    private static MoeDbContext CreateDbContext()
    {
        DbContextOptions<MoeDbContext> options = new DbContextOptionsBuilder<MoeDbContext>()
            .UseInMemoryDatabase($"identity-sgt-red-{Guid.NewGuid():N}")
            .Options;
        return new MoeDbContext(options, [new IdentityPlatformModelConfiguration()]);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;

        public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);
    }
}
