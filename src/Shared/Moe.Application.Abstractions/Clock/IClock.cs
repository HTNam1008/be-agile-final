namespace Moe.Application.Abstractions.Clock;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    DateOnly TodayInSingapore();
}

public static class SingaporeBusinessDay
{
    private static readonly TimeSpan SingaporeOffset = TimeSpan.FromHours(8);

    public static DateOnly FromUtc(DateTime utcNow)
    {
        DateTime normalizedUtc = utcNow.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
            : utcNow.ToUniversalTime();

        return DateOnly.FromDateTime(normalizedUtc.Add(SingaporeOffset));
    }

    public static DateOnly FromUtc(DateTimeOffset utcNow)
        => DateOnly.FromDateTime(utcNow.ToUniversalTime().ToOffset(SingaporeOffset).DateTime);
}
