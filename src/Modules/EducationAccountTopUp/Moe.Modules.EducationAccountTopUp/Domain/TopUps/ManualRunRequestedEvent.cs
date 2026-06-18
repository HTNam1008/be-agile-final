using Moe.SharedKernel.Domain;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public sealed record ManualRunRequestedEvent(
    long TopUpRunId,
    long CampaignId,
    long RequestedByUserId,
    DateTime OccurredAt) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; } = new DateTimeOffset(
        DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc));
}

public static class TopUpErrors
{
    public static readonly Moe.SharedKernel.Results.Error Unauthorized =
        new("TopUp.Unauthorized", "Insufficient permissions to execute top-up runs.");

    public static readonly Moe.SharedKernel.Results.Error CampaignNotFound =
        new("TopUp.CampaignNotFound", "Campaign not found.");

    public static readonly Moe.SharedKernel.Results.Error CampaignNotExecutable =
        new("TopUp.CampaignNotExecutable", "Campaign is not in an executable state.");

    public static readonly Moe.SharedKernel.Results.Error ActorRequired =
        new("TopUp.ActorRequired", "An authenticated admin is required.");

    public static readonly Moe.SharedKernel.Results.Error InvalidRunTransition =
        new("TopUp.InvalidRunTransition", "The requested status transition is not valid for the current run state.");

    public static readonly Moe.SharedKernel.Results.Error RunIsTerminal =
        new("TopUp.RunIsTerminal", "This run has already reached a terminal state and cannot be modified.");

    public static readonly Moe.SharedKernel.Results.Error DuplicateScheduledOccurrence =
        new("TopUp.DuplicateScheduledOccurrence", "A run for this campaign and scheduled time already exists.");

    public static readonly Moe.SharedKernel.Results.Error ReconciliationMismatch =
        new("TopUp.ReconciliationMismatch", "Reconciliation counts do not add up: totalProcessed must equal totalSucceeded + totalFailed + totalSkipped.");

    public static readonly Moe.SharedKernel.Results.Error TransactionIsTerminal =
        new("TopUp.TransactionIsTerminal", "This recipient transaction has already reached a terminal state and cannot be modified.");

    public static readonly Moe.SharedKernel.Results.Error TransactionNotPending =
        new("TopUp.TransactionNotPending", "Only pending transactions can be transitioned.");

    public static readonly Moe.SharedKernel.Results.Error DuplicateRecipientTransaction =
        new("TopUp.DuplicateRecipientTransaction", "A transaction for this recipient in this run already exists.");

    public static readonly Moe.SharedKernel.Results.Error InvalidAccountTransactionReference =
        new("TopUp.InvalidAccountTransactionReference", "AccountTransactionId must be a valid positive reference.");

    public static readonly Moe.SharedKernel.Results.Error TransactionReasonRequired =
        new("TopUp.TransactionReasonRequired", "A safe display reason is required.");

    public static readonly Moe.SharedKernel.Results.Error CreditServiceUnavailable =
        new("TopUp.CreditServiceUnavailable", "Account credit service is temporarily unavailable.");

    public static readonly Moe.SharedKernel.Results.Error RecipientNotEligible =
        new("TopUp.RecipientNotEligible", "Recipient is not eligible for top-up credit.");

    public static readonly Moe.SharedKernel.Results.Error InvalidCreditAmount =
        new("TopUp.InvalidCreditAmount", "Credit amount must be positive.");

    public static readonly Moe.SharedKernel.Results.Error AccountNotFound =
        new("TopUp.AccountNotFound", "Education account not found.");

    public static readonly Moe.SharedKernel.Results.Error AccountNotActive =
        new("TopUp.AccountNotActive", "Education account is not in active status.");

    public static readonly Moe.SharedKernel.Results.Error RunNotFound =
        new("TopUp.RunNotFound", "Top-up run not found.");

    public static readonly Moe.SharedKernel.Results.Error RunAlreadyTerminal =
        new("TopUp.RunAlreadyTerminal", "Run has already reached a terminal state and cannot be executed.");

    public static readonly Moe.SharedKernel.Results.Error NonPositiveDefaultAmount =
        new("TopUp.NonPositiveDefaultAmount", "Campaign default top-up amount must be positive.");
}
