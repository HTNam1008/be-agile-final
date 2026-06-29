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
        ITopUpCampaignRepository campaigns = services.GetRequiredService<ITopUpCampaignRepository>();
        ITopUpTransactionRepository transactions = services.GetRequiredService<ITopUpTransactionRepository>();
        IRecipientResolver recipientResolver = services.GetRequiredService<IRecipientResolver>();
        IRunExecutionOrchestrator orchestrator = services.GetRequiredService<IRunExecutionOrchestrator>();
        IRunReconciliationService reconciliation = services.GetRequiredService<IRunReconciliationService>();
        IPendingTransactionRecoveryService recovery = services.GetRequiredService<IPendingTransactionRecoveryService>();
        IUnitOfWork unitOfWork = services.GetRequiredService<IUnitOfWork>();
        IDynamicTopUpContractRepository contractRepo = services.GetRequiredService<IDynamicTopUpContractRepository>();
        var dbContext = services.GetService<Microsoft.EntityFrameworkCore.DbContext>();

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
            await ProcessContractDrivenRunAsync(run, campaigns, contractRepo, orchestrator, recovery, reconciliation, unitOfWork, cancellationToken);
            return;
        }

        TopUpCampaign? campaign = await campaigns.GetByIdAsync(run.TopUpCampaignId, cancellationToken);
        bool isInstant = string.Equals(campaign?.DeliveryTypeCode, "INSTANT", StringComparison.OrdinalIgnoreCase);
        decimal? maxTotalAmount = (!isInstant && campaign?.MaxTotalAmount > 0) ? campaign.MaxTotalAmount : null;

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

        if (maxTotalAmount.HasValue && campaign is not null)
        {
            decimal alreadyDisbursed = await transactions.GetTotalDisbursedForCampaignAsync(campaign.Id, cancellationToken);
            decimal resolvedAmount = await recipientResolver.GetTotalResolvedAmountAsync(run.TopUpCampaignId, runId, cancellationToken);
            decimal projectedTotal = alreadyDisbursed + resolvedAmount;

            if (projectedTotal > maxTotalAmount.Value)
            {
                logger.LogWarning(
                    "Top-up run {RunId} would exceed budget cap: already disbursed {AlreadyDisbursed} + projected {Projected} = {Total} > cap {Cap}",
                    runId, alreadyDisbursed, projectedTotal, alreadyDisbursed + projectedTotal, maxTotalAmount.Value);

                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsSuccess)
                {
                    run.ReconcileCounters(0, 0, 0, 0, 0m);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return;
            }

            bool budgetReserved = await transactions.TryReserveBudgetAsync(campaign.Id, resolvedAmount, maxTotalAmount.Value, cancellationToken);
            if (!budgetReserved)
            {
                logger.LogWarning(
                    "Top-up run {RunId} failed to reserve budget: already disbursed {AlreadyDisbursed} + requested {Requested} would exceed cap {Cap}",
                    runId, alreadyDisbursed, resolvedAmount, maxTotalAmount.Value);

                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsSuccess)
                {
                    run.ReconcileCounters(0, 0, 0, 0, 0m);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }

                return;
            }
        }

        Result<RunExecutionResult> execution = await ExecuteRunStreamedAsync(
            run,
            totalRecipients,
            maxTotalAmount,
            runs,
            recipientResolver,
            orchestrator,
            unitOfWork,
            dbContext,
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

    private async Task<Result<RunExecutionResult>> ExecuteRunStreamedAsync(
        TopUpRun run,
        int totalRecipients,
        decimal? maxTotalAmount,
        ITopUpRunRepository runs,
        IRecipientResolver recipientResolver,
        IRunExecutionOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        Microsoft.EntityFrameworkCore.DbContext? dbContext,
        CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        orchestrator.RegisterCancellationToken(run.Id, linkedCts);

        try
        {
            Result startProcessing = run.StartProcessing(clock.UtcNow.UtcDateTime);
            if (startProcessing.IsFailure)
            {
                return Result<RunExecutionResult>.Failure(startProcessing.Error);
            }

            Result setSelected = run.SetTotalSelected(totalRecipients);
            if (setSelected.IsFailure)
            {
                return Result<RunExecutionResult>.Failure(setSelected.Error);
            }

            await unitOfWork.SaveChangesAsync(ct);

            int offset = 0;
            var accumulator = new ChunkProcessingAccumulator();
            int cancelCheckCounter = 0;

            while (offset < totalRecipients)
            {
                if (linkedCts.Token.IsCancellationRequested || run.IsCancelRequested)
                {
                    break;
                }

                cancelCheckCounter++;
                if (cancelCheckCounter % 10 == 0)
                {
                    TopUpRun? refreshedRun = await runs.GetByIdAsync(run.Id, ct);
                    if (refreshedRun is not null && refreshedRun.IsCancelRequested)
                    {
                        run.RequestCancel(clock.UtcNow.UtcDateTime);
                        logger.LogInformation(
                            "Top-up run {RunId} cancel request detected from database; stopping execution",
                            run.Id);
                        break;
                    }
                }

                IReadOnlyList<RecipientInfo> chunk = await recipientResolver.GetRecipientsChunkAsync(
                    run.TopUpCampaignId,
                    run.Id,
                    ChunkSize,
                    offset,
                    ct);

                if (chunk.Count == 0)
                {
                    break;
                }

                if (maxTotalAmount.HasValue && accumulator.TotalAmount >= maxTotalAmount.Value)
                {
                    logger.LogInformation(
                        "Top-up run {RunId} reached MaxTotalAmount cap {MaxTotalAmount}; skipping remaining {Remaining} recipients",
                        run.Id, maxTotalAmount.Value, totalRecipients - offset);
                    break;
                }

                IReadOnlyList<RecipientInfo> cappedChunk = CapChunkToBudget(chunk, maxTotalAmount, accumulator.TotalAmount);

                Result<ChunkProcessingResult> chunkResult = await orchestrator.ProcessChunkAsync(
                    run.Id,
                    cappedChunk,
                    accumulator,
                    linkedCts.Token);

                if (chunkResult.IsFailure)
                {
                    return Result<RunExecutionResult>.Failure(chunkResult.Error);
                }

                offset += chunk.Count;

            await unitOfWork.SaveChangesAsync(ct);
            dbContext?.ChangeTracker.Clear();

                logger.LogInformation(
                    "Top-up run {RunId} processed chunk {Offset}/{TotalRecipients}: succeeded={Succeeded}, failed={Failed}, skipped={Skipped}",
                    run.Id,
                    offset,
                    totalRecipients,
                    accumulator.TotalSucceeded,
                    accumulator.TotalFailed,
                    accumulator.TotalSkipped);
            }

            int totalProcessed = accumulator.TotalProcessed;

            if (linkedCts.Token.IsCancellationRequested)
            {
                Result cancelResult = run.Cancel(clock.UtcNow.UtcDateTime);
                if (cancelResult.IsFailure)
                {
                    return Result<RunExecutionResult>.Failure(cancelResult.Error);
                }
                run.ReconcileCounters(totalProcessed, accumulator.TotalSucceeded, accumulator.TotalFailed, accumulator.TotalSkipped, accumulator.TotalAmount);
            }
            else
            {
                Result finalize = run.Finalize(
                    totalProcessed,
                    accumulator.TotalSucceeded,
                    accumulator.TotalFailed,
                    accumulator.TotalSkipped,
                    accumulator.TotalAmount,
                    clock.UtcNow.UtcDateTime);

                if (finalize.IsFailure)
                {
                    return Result<RunExecutionResult>.Failure(finalize.Error);
                }
            }

            await unitOfWork.SaveChangesAsync(ct);

            return Result<RunExecutionResult>.Success(new RunExecutionResult
            {
                TopUpRunId = run.Id,
                TerminalStatus = run.RunStatusCode,
                TotalSelected = totalRecipients,
                TotalProcessed = totalProcessed,
                TotalSucceeded = accumulator.TotalSucceeded,
                TotalFailed = accumulator.TotalFailed,
                TotalSkipped = accumulator.TotalSkipped,
                TotalAmount = accumulator.TotalAmount,
                SuccessfulAccountIds = accumulator.SuccessfulAccountIds
            });
        }
        finally
        {
            orchestrator.UnregisterCancellationToken(run.Id);
        }
    }

    private async Task ProcessContractDrivenRunAsync(
        TopUpRun run,
        ITopUpCampaignRepository campaigns,
        IDynamicTopUpContractRepository contractRepo,
        IRunExecutionOrchestrator orchestrator,
        IPendingTransactionRecoveryService recovery,
        IRunReconciliationService reconciliation,
        IUnitOfWork unitOfWork,
        CancellationToken ct)
    {
        DateTime nowUtc = clock.UtcNow.UtcDateTime;

        await recovery.RecoverPendingTransactionsAsync(
            run.Id,
            run.Note ?? "Contract-driven top-up run",
            ct);

        var dueContracts = await contractRepo.GetDueForPaymentAsync(nowUtc, ct);
        var campaignContracts = dueContracts.Where(c => c.TopUpCampaignId == run.TopUpCampaignId).ToList();

        var payments = campaignContracts.Select(c => (
            Contract: c,
            ActualAmount: Math.Min(c.AmountPerPayment, c.MaxTotalAmount - c.TotalReceived)
        )).ToList();

        var recipients = payments.Select(p => new RecipientInfo
        {
            EducationAccountId = p.Contract.EducationAccountId,
            Amount = p.ActualAmount,
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

            foreach (var payment in payments)
            {
                var contract = payment.Contract;
                if (execution.Value.SuccessfulAccountIds.Contains(contract.EducationAccountId))
                {
                    contract.RecordPayment(payment.ActualAmount, nowUtc);

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
            }

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

    private static IReadOnlyList<RecipientInfo> CapChunkToBudget(
        IReadOnlyList<RecipientInfo> chunk,
        decimal? maxTotalAmount,
        decimal currentTotal)
    {
        if (!maxTotalAmount.HasValue)
        {
            return chunk;
        }

        decimal remaining = maxTotalAmount.Value - currentTotal;
        if (remaining <= 0)
        {
            return Array.Empty<RecipientInfo>();
        }

        List<RecipientInfo> capped = [];
        decimal runningTotal = 0m;

        foreach (RecipientInfo recipient in chunk)
        {
            decimal available = remaining - runningTotal;
            if (available <= 0)
            {
                break;
            }

            decimal cappedAmount = Math.Min(recipient.Amount, available);
            if (cappedAmount <= 0)
            {
                break;
            }

            capped.Add(recipient with { Amount = cappedAmount });
            runningTotal += cappedAmount;
        }

        return capped;
    }
}
