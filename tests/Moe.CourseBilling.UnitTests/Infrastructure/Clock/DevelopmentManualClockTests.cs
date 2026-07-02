using FluentAssertions;
using Moe.Infrastructure.Shared.Clock;
using Xunit;

namespace Moe.CourseBilling.UnitTests.Infrastructure.Clock;

public sealed class DevelopmentManualClockTests
{
    [Fact]
    public void Set_Should_Normalize_To_Utc()
    {
        DevelopmentManualClock clock = new();

        clock.Set(new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.FromHours(8)));

        clock.IsOverridden.Should().BeTrue();
        clock.UtcNow.Should().Be(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Advance_Should_Move_Overridden_Time()
    {
        DevelopmentManualClock clock = new();
        clock.Set(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        DateTimeOffset advanced = clock.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(30));

        advanced.Should().Be(new DateTimeOffset(2026, 7, 1, 2, 30, 0, TimeSpan.Zero));
        clock.UtcNow.Should().Be(advanced);
    }

    [Fact]
    public void Reset_Should_Return_To_System_Time()
    {
        DevelopmentManualClock clock = new();
        clock.Set(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        clock.Reset();

        clock.IsOverridden.Should().BeFalse();
        clock.UtcNow.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TodayInSingapore_Should_Use_Overridden_Utc_Time()
    {
        DevelopmentManualClock clock = new();
        clock.Set(new DateTimeOffset(2026, 6, 30, 16, 30, 0, TimeSpan.Zero));

        clock.TodayInSingapore().Should().Be(new DateOnly(2026, 7, 1));
    }
}
