using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.Lifecycle;

public sealed class EducationAccountLifecycleRun : AggregateRoot<long>
{
    private readonly List<EducationAccountLifecycleRunItem> _items = [];

    private EducationAccountLifecycleRun() : base(0) { }

    private EducationAccountLifecycleRun(
        DateOnly runDateUtc,
        DateTimeOffset startedAtUtc,
        string triggerTypeCode) : base(0)
    {
        RunDateUtc = runDateUtc;
        StartedAtUtc = startedAtUtc;
        TriggerTypeCode = triggerTypeCode;
        StatusCode = EducationAccountLifecycleRunStatusCodes.Running;
    }

    public DateOnly RunDateUtc { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string TriggerTypeCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public int OpenedCount { get; private set; }
    public int ClosedCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyCollection<EducationAccountLifecycleRunItem> Items => _items;

    public static EducationAccountLifecycleRun Start(
        DateOnly runDateUtc,
        DateTimeOffset startedAtUtc,
        string triggerTypeCode)
        => new(runDateUtc, startedAtUtc, triggerTypeCode);

    public void AddItem(
        long personId,
        long educationAccountId,
        string actionCode,
        DateTimeOffset occurredAtUtc)
    {
        _items.Add(new EducationAccountLifecycleRunItem(
            Id: 0,
            RunId: Id,
            personId,
            educationAccountId,
            actionCode,
            occurredAtUtc));
    }

    public void Complete(
        int openedCount,
        int closedCount,
        DateTimeOffset completedAtUtc)
    {
        OpenedCount = openedCount;
        ClosedCount = closedCount;
        CompletedAtUtc = completedAtUtc;
        StatusCode = EducationAccountLifecycleRunStatusCodes.Completed;
        ErrorMessage = null;
    }

    public void Fail(
        string errorMessage,
        DateTimeOffset failedAtUtc)
    {
        CompletedAtUtc = failedAtUtc;
        StatusCode = EducationAccountLifecycleRunStatusCodes.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Lifecycle run failed."
            : errorMessage.Trim();
    }
}

public sealed class EducationAccountLifecycleRunItem : Entity<long>
{
    private EducationAccountLifecycleRunItem() : base(0) { }

    internal EducationAccountLifecycleRunItem(
        long Id,
        long RunId,
        long personId,
        long educationAccountId,
        string actionCode,
        DateTimeOffset occurredAtUtc) : base(Id)
    {
        EducationAccountLifecycleRunId = RunId;
        PersonId = personId;
        EducationAccountId = educationAccountId;
        ActionCode = actionCode;
        OccurredAtUtc = occurredAtUtc;
    }

    public long EducationAccountLifecycleRunId { get; private set; }
    public long PersonId { get; private set; }
    public long EducationAccountId { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; private set; }
}

public static class EducationAccountLifecycleRunTriggerTypes
{
    public const string Scheduled = "SCHEDULED";
    public const string Manual = "MANUAL";
}

public static class EducationAccountLifecycleRunStatusCodes
{
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
}

public static class EducationAccountLifecycleRunItemActionCodes
{
    public const string Created = "CREATED";
    public const string Closed = "CLOSED";
}
