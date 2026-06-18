namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed record TransactionSummary
{
    public required int TotalSelected { get; init; }
    public required int TotalProcessed { get; init; }
    public required int TotalSucceeded { get; init; }
    public required int TotalFailed { get; init; }
    public required int TotalSkipped { get; init; }
    public required int TotalPending { get; init; }
    public required decimal TotalAmount { get; init; }
}
