using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Persistence;
using Moe.Modules.EducationAccountTopUp.Domain.TopUps;
using Moe.Modules.EducationAccountTopUp.IGateway;
using Moe.Modules.EducationAccountTopUp.IGateway.Repositories;
using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Application.RunExecution;

public sealed class TopUpRunWorker(
    ITopUpRunQueueReader queueReader,
    IServiceScopeFactory scopeFactory,
    ILogger<TopUpRunWorker> logger,
    IClock clock) : BackgroundService
{
    public const int ChunkSize = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Top-up run worker started");

        try
        {
            await foreach (long runId in queueReader.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessRunAsync(runId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("Top-up run worker stopping while processing run {TopUpRunId}", runId);
                    return;
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Top-up run worker stopped");
        }
    }

    public async Task ProcessRunAsync(long runId, CancellationToken cancellationToken = default)
    {
        using IDisposable? correlationScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = $"topup-run-{runId}"
        });

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

        if (run.IsContractDriven)
        {
            await ProcessContractDrivenRunAsync(run, runs, orchestrator, recovery, reconciliation, cancellationToken);
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

    private async Task ProcessContractDrivenRunAsync(
        TopUpRun run,
        ITopUpRunRepository runs,
        IRunExecutionOrchestrator orchestrator,
        IPendingTransactionRecoveryService recovery,
        IRunReconciliationService reconciliation,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var contractRepo = scope.ServiceProvider.GetRequiredService<IDynamicTopUpContractRepository>();

        DateTime nowUtc = clock.UtcNow.UtcDateTime;

        await recovery.RecoverPendingTransactionsAsync(
            run.Id,
            run.Note ?? "Contract-driven top-up run",
            ct);

        var dueContracts = await contractRepo.GetDueForPaymentAsync(nowUtc, ct);
        var campaignContracts = dueContracts.Where(c => c.TopUpCampaignId == run.TopUpCampaignId).ToList();

        var recipients = campaignContracts.Select(c => new RecipientInfo
        {
            EducationAccountId = c.EducationAccountId,
            Amount = c.AmountPerPayment,
            OrganizationUnitId = 0,
            CampaignReason = "Contract-driven top-up"
        }).ToList();

        logger.LogInformation(
            "Contract-driven run {RunId} processing {Count} due contracts for campaign {CampaignId}",
            run.Id, campaignContracts.Count, run.TopUpCampaignId);

        Result<RunExecutionResult> execution = await orchestrator.ExecuteRunAsync(
            run.Id,
            recipients,
            ct);

        if (execution.IsSuccess)
        {
            logger.LogInformation(
                "Contract-driven run {RunId} completed: succeeded={Succeeded}, failed={Failed}",
                run.Id,
                execution.Value.TotalSucceeded,
                execution.Value.TotalFailed);

            foreach (var contract in campaignContracts)
            {
                if (contract.DeliveryTypeCode == DeliveryType.FixedContract && !contract.IsCompleted)
                {
                    DateTime? nextPaymentDate = RecurrenceCalculator.CalculateNextRun(
                        contract.FrequencyCode,
                        contract.FrequencyInterval,
                        contract.NextPaymentDate!.Value,
                        null);
                    contract.SetNextPaymentDate(nextPaymentDate, nowUtc);
                }
            }

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.SaveChangesAsync(ct);
        }
        else
        {
            logger.LogError(
                "Contract-driven run {RunId} failed: {ErrorCode}",
                run.Id,
                execution.Error.Code);
        }

        Result<ReconciliationResult> reconciliationResult = await reconciliation.ReconcileRunAsync(
            run.Id, ct);

        if (reconciliationResult.IsSuccess)
        {
            logger.LogInformation(
                "Contract-driven run {RunId} reconciliation: {ReconciliationStatus}",
                run.Id,
                reconciliationResult.Value.ReconciliationStatus);
        }
        else
        {
            logger.LogWarning(
                "Contract-driven run {RunId} reconciliation failed: {ErrorCode}",
                run.Id,
                reconciliationResult.Error.Code);
        }
    }
}
