using Moe.Application.Abstractions.Clock;

namespace Moe.Infrastructure.Shared.Clock;

public sealed class DevelopmentManualClock : IClock
{
    private readonly object sync = new();
    private DateTimeOffset? overriddenUtcNow;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (sync)
            {
                return overriddenUtcNow ?? DateTimeOffset.UtcNow;
            }
        }
    }

    public DateOnly TodayInSingapore() => SingaporeBusinessDay.FromUtc(UtcNow);

    public bool IsOverridden
    {
        get
        {
            lock (sync)
            {
                return overriddenUtcNow.HasValue;
            }
        }
    }

    public void Set(DateTimeOffset utcNow)
    {
        lock (sync)
        {
            overriddenUtcNow = utcNow.ToUniversalTime();
        }
    }

    public DateTimeOffset Advance(TimeSpan delta)
    {
        lock (sync)
        {
            DateTimeOffset nextUtcNow = (overriddenUtcNow ?? DateTimeOffset.UtcNow).Add(delta).ToUniversalTime();
            overriddenUtcNow = nextUtcNow;
            return nextUtcNow;
        }
    }

    public void Reset()
    {
        lock (sync)
        {
            overriddenUtcNow = null;
        }
    }
}
