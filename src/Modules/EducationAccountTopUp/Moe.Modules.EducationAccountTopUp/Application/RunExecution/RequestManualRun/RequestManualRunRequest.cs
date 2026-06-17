namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution.RequestManualRun;

public sealed record RequestManualRunRequest
{
    public required string IdempotencyKey { get; init; }
    public string? Note { get; init; }
}
