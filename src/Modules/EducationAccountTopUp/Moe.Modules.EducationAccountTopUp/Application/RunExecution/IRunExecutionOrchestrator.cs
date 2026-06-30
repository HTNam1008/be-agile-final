using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public interface IRunExecutionOrchestrator
{
    Task<Result<RunExecutionResult>> ExecuteRunAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> recipients,
        CancellationToken cancellationToken = default);

    Task<Result<ChunkProcessingResult>> ProcessChunkAsync(
        long topUpRunId,
        IReadOnlyList<RecipientInfo> chunk,
        ChunkProcessingAccumulator accumulator,
        CancellationToken cancellationToken = default);

    void RegisterCancellationToken(long topUpRunId, CancellationTokenSource cts);
    void UnregisterCancellationToken(long topUpRunId);
    bool CancelRun(long topUpRunId);
}

public sealed record ChunkProcessingResult(
    int ChunkSucceeded,
    int ChunkFailed,
    int ChunkSkipped,
    decimal ChunkAmount,
    IReadOnlyList<long> ChunkSuccessfulAccountIds);

public sealed class ChunkProcessingAccumulator
{
    public int TotalSucceeded { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
    public decimal TotalAmount { get; set; }
    public List<long> SuccessfulAccountIds { get; } = [];
    public int TotalProcessed => TotalSucceeded + TotalFailed + TotalSkipped;
}
