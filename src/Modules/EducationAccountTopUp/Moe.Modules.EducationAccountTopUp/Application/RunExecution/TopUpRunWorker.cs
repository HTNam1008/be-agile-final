using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class TopUpRunWorker(
    ITopUpRunQueueReader queueReader,
    IServiceScopeFactory scopeFactory,
    ILogger<TopUpRunWorker> logger) : BackgroundService
{
    public const int ChunkSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Top-up run worker started");

        await foreach (long runId in queueReader.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessRunAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled error while processing top-up run {TopUpRunId}",
                    runId);
            }
        }
    }

    public async Task ProcessRunAsync(long runId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Top-up worker picked up run {TopUpRunId}", runId);

        using IServiceScope scope = scopeFactory.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ITopUpRunRepository runs = services.GetRequiredService<ITopUpRunRepository>();
        IRecipientResolver recipientResolver = services.GetRequiredService<IRecipientResolver>();
        IRunExecutionOrchestrator orchestrator = services.GetRequiredService<IRunExecutionOrchestrator>();
        IRunReconciliationService reconciliation = services.GetRequiredService<IRunReconciliationService>();
        IPendingTransactionRecoveryService recovery = services.GetRequiredService<IPendingTransactionRecoveryService>();

        TopUpRun? run = await runs.GetByIdAsync(runId, cancellationToken);
        if (run is null)
        {
            logger.LogWarning("Top-up run {TopUpRunId} not found; skipping", runId);
            return;
        }

        if (TopUpRunStatusCodes.TerminalStatuses.Contains(run.RunStatusCode))
        {
            logger.LogInformation(
                "Top-up run {TopUpRunId} is already terminal ({RunStatusCode}); skipping",
                runId,
                run.RunStatusCode);
            return;
        }

        await recovery.RecoverPendingTransactionsAsync(
            runId,
            run.Note ?? "Top-up run execution",
            cancellationToken);

        int totalRecipients = await recipientResolver.GetTotalRecipientCountAsync(
            run.TopUpCampaignId,
            runId,
            cancellationToken);

        logger.LogInformation(
            "Top-up run {TopUpRunId} has {TotalRecipients} recipients to process in chunks of {ChunkSize}",
            runId,
            totalRecipients,
            ChunkSize);

        List<RecipientInfo> recipients = [];
        int offset = 0;

        while (offset < totalRecipients)
        {
            IReadOnlyList<RecipientInfo> chunk = await recipientResolver.GetRecipientsChunkAsync(
                run.TopUpCampaignId,
                runId,
                ChunkSize,
                offset,
                cancellationToken);

            if (chunk.Count == 0)
            {
                break;
            }

            recipients.AddRange(chunk);
            offset += chunk.Count;

            logger.LogInformation(
                "Top-up run {TopUpRunId} resolved {ResolvedCount}/{TotalRecipients} recipients",
                runId,
                offset,
                totalRecipients);
        }

        Result<RunExecutionResult> execution = await orchestrator.ExecuteRunAsync(
            runId,
            recipients,
            cancellationToken);

        if (execution.IsSuccess)
        {
            logger.LogInformation(
                "Top-up run {TopUpRunId} execution completed with status {RunStatusCode}; succeeded={Succeeded}, failed={Failed}, skipped={Skipped}, amount={Amount}",
                runId,
                execution.Value.TerminalStatus,
                execution.Value.TotalSucceeded,
                execution.Value.TotalFailed,
                execution.Value.TotalSkipped,
                execution.Value.TotalAmount);
        }
        else
        {
            logger.LogError(
                "Top-up run {TopUpRunId} execution failed: {ErrorCode}",
                runId,
                execution.Error.Code);
        }

        Result<ReconciliationResult> reconciliationResult = await reconciliation.ReconcileRunAsync(
            runId,
            cancellationToken);

        if (reconciliationResult.IsSuccess)
        {
            logger.LogInformation(
                "Top-up run {TopUpRunId} reconciliation status: {ReconciliationStatus}",
                runId,
                reconciliationResult.Value.ReconciliationStatus);
        }
        else
        {
            logger.LogWarning(
                "Top-up run {TopUpRunId} reconciliation failed: {ErrorCode}",
                runId,
                reconciliationResult.Error.Code);
        }
    }
}
