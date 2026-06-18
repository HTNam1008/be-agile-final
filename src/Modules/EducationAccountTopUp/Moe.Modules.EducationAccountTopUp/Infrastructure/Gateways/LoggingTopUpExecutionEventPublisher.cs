using Microsoft.Extensions.Logging;
using Moe.Modules.EducationAccountTopUp.IGateway;

namespace Moe.Modules.EducationAccountTopUp.Infrastructure.Gateways;

internal sealed class LoggingTopUpExecutionEventPublisher(
    ILogger<LoggingTopUpExecutionEventPublisher> logger) : ITopUpExecutionEventPublisher
{
    public Task PublishRunStartedAsync(
        TopUpRunStartedReport report,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "TopUpRunStarted emitted for run {TopUpRunId}, campaign {CampaignId}, totalSelected={TotalSelected}",
            report.TopUpRunId,
            report.CampaignId,
            report.TotalSelected);

        return Task.CompletedTask;
    }

    public Task PublishRunCompletedAsync(
        TopUpRunCompletedReport report,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "TopUpRunCompleted emitted for run {TopUpRunId}, campaign {CampaignId}, status={TerminalStatus}, processed={TotalProcessed}, succeeded={TotalSucceeded}, failed={TotalFailed}, skipped={TotalSkipped}, amount={TotalAmount}",
            report.TopUpRunId,
            report.CampaignId,
            report.TerminalStatus,
            report.TotalProcessed,
            report.TotalSucceeded,
            report.TotalFailed,
            report.TotalSkipped,
            report.TotalAmount);

        return Task.CompletedTask;
    }

    public Task PublishTopUpReceivedAsync(
        TopUpReceivedReport report,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "TopUpReceived emitted for run {TopUpRunId}, top-up transaction {TopUpTransactionId}, account {EducationAccountId}, account transaction {AccountTransactionId}, amount={Amount}, alreadyProcessed={AlreadyProcessed}",
            report.TopUpRunId,
            report.TopUpTransactionId,
            report.EducationAccountId,
            report.AccountTransactionId,
            report.Amount,
            report.AlreadyProcessed);

        return Task.CompletedTask;
    }
}
