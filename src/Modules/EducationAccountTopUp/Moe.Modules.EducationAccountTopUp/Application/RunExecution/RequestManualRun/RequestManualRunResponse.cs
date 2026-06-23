namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed record RequestManualRunResponse
{
    public required long RunId { get; init; }
    public required string Status { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
}
