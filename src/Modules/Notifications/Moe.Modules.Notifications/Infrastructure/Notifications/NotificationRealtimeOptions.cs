namespace Moe.Modules.Notifications.Infrastructure.Notifications;

public sealed class NotificationRealtimeOptions
{
    public const string SectionName = "Notifications:Realtime";

    public bool Enabled { get; init; } = true;

    public NotificationRealtimeWorkerOptions Worker { get; init; } = new();

    public static bool IsValid(NotificationRealtimeOptions options)
        => options.Worker.BatchSize is >= 1 and <= 100
           && options.Worker.PollIntervalSeconds is >= 1 and <= 300
           && options.Worker.LockSeconds is >= 30 and <= 3600
           && options.Worker.MaxAttempts is >= 1 and <= 20;
}

public sealed class NotificationRealtimeWorkerOptions
{
    public bool Enabled { get; init; } = true;

    public int BatchSize { get; init; } = 25;

    public int PollIntervalSeconds { get; init; } = 2;

    public int LockSeconds { get; init; } = 60;

    public int MaxAttempts { get; init; } = 5;
}
