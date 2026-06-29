using Moe.SharedKernel.Results;

namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class TopUpErrors
{
    // Authorization & Scope Errors
    public static readonly Error Unauthorized =
        new("TopUp.Unauthorized", "Insufficient permissions to execute top-up runs.");

    public static readonly Error ActorRequired =
        new("TopUp.ActorRequired", "An authenticated admin is required.");

    public static readonly Error AdminOrganizationScopeRequired =
        new("TopUp.AdminOrganizationScopeRequired", "An admin organization scope is required.");

    public static readonly Error OrganizationOutsideScope =
        new("TopUp.OrganizationOutsideScope", "The requested organization is outside the admin's scope.");

    public static readonly Error AccountSelectionOutsideScope =
        new("TopUp.AccountSelectionOutsideScope", "The top-up account selection contains accounts outside the admin's scope or filter.");

    // Campaign Errors
    public static readonly Error CampaignNotFound =
        new("TopUp.CampaignNotFound", "The top-up campaign was not found.");

    public static readonly Error CampaignNotExecutable =
        new("TopUp.CampaignNotExecutable", "Campaign is not in an executable state.");

    public static readonly Error InvalidCampaignStatus =
        new("TopUp.InvalidCampaignStatus", "Recipients and rules can only be modified for a draft or paused campaign.");

    public static readonly Error InvalidStatusTransition =
        new("TopUp.InvalidStatusTransition", "Cannot transition campaign to the requested status.");

    public static readonly Error InvalidRecipientMode =
        new("TopUp.InvalidRecipientMode", "Recipients can only be selected directly for a fixed-selection campaign.");

    public static readonly Error RulesOnlyForDynamic =
        new("TopUp.RulesOnlyForDynamic", "Rules can only be added to DYNAMIC_RULES campaigns.");

    public static readonly Error ConcurrencyException =
        new("TopUp.ConcurrencyException", "The campaign has been modified by another process.");

    public static readonly Error NonPositiveDefaultAmount =
        new("TopUp.NonPositiveDefaultAmount", "Campaign default top-up amount must be positive.");

    public static readonly Error EmptyDynamicRules =
        new("TopUp.EmptyDynamicRules", "Cannot activate a DYNAMIC_RULES campaign with zero active rules.");

    public static readonly Error EmptyFixedRecipients =
        new("TopUp.EmptyFixedRecipients", "Cannot activate a FIXED_SELECTION campaign with zero recipients.");

    public static readonly Error PreviewNoRules =
        new("TopUp.PreviewNoRules", "Cannot preview a dynamic campaign that has no rules configured.");

    public static readonly Error ManualRunDisabled =
        new("TopUp.ManualRunDisabled", "Manual runs are only allowed for IMMEDIATE campaigns. Scheduled or recurring campaigns must run automatically.");

    public static readonly Error CampaignAlreadyExecuted =
        new("TopUp.CampaignAlreadyExecuted", "Immediate campaign has already been executed successfully.");

    public static readonly Error DateMismatch =
        new("TopUp.DateMismatch", "End date must be greater than or equal to start date.");

    public static readonly Error CannotUpdateActiveCampaign =
        new("TopUp.CannotUpdateActiveCampaign", "Campaign configuration cannot be modified after activation.");

    public static readonly Error CannotChangeMaxTotalAmountAfterActive =
        new("TopUp.CannotChangeMaxTotalAmountAfterActive", "MaxTotalAmount is immutable once campaign is active. Budget caps cannot be altered after execution begins.");

    public static readonly Error MaxTotalAmountBelowPerPayment =
        new("TopUp.MaxTotalAmountBelowPerPayment", "MaxTotalAmount must be greater than or equal to DefaultTopUpAmount.");

    public static readonly Error InstantRequiresExactMax =
        new("TopUp.InstantRequiresExactMax", "Instant delivery campaigns must have MaxTotalAmount equal to DefaultTopUpAmount.");

    // Contract Errors
    public static readonly Error ContractAlreadyCompleted =
        new("TopUp.ContractAlreadyCompleted", "Contract has already reached a terminal state.");
    public static readonly Error ContractNotFound =
        new("TopUp.ContractNotFound", "Dynamic top-up contract not found.");
    public static readonly Error ContractNotActive =
        new("TopUp.ContractNotActive", "Contract is not in active status.");

    // Run Errors
    public static readonly Error RunNotFound =
        new("TopUp.RunNotFound", "Top-up run not found.");

    public static readonly Error InvalidRunTransition =
        new("TopUp.InvalidRunTransition", "The requested status transition is not valid for the current run state.");

    public static readonly Error RunIsTerminal =
        new("TopUp.RunIsTerminal", "This run has already reached a terminal state and cannot be modified.");

    public static readonly Error RunAlreadyTerminal =
        new("TopUp.RunAlreadyTerminal", "Run has already reached a terminal state and cannot be executed.");

    public static readonly Error DuplicateScheduledOccurrence =
        new("TopUp.DuplicateScheduledOccurrence", "A run for this campaign and scheduled time already exists.");

    public static readonly Error ReconciliationMismatch =
        new("TopUp.ReconciliationMismatch", "Reconciliation counts do not add up: totalProcessed must equal totalSucceeded + totalFailed + totalSkipped.");

    // Transaction & Account Errors
    public static readonly Error AccountNotFound =
        new("TopUp.AccountNotFound", "Education account not found.");

    public static readonly Error AccountNotActive =
        new("TopUp.AccountNotActive", "Education account is not in active status.");

    public static readonly Error InvalidAccountSelection =
        new("TopUp.InvalidAccountSelection", "The top-up account selection is invalid.");

    public static readonly Error TransactionIsTerminal =
        new("TopUp.TransactionIsTerminal", "This recipient transaction has already reached a terminal state and cannot be modified.");

    public static readonly Error TransactionNotPending =
        new("TopUp.TransactionNotPending", "Only pending transactions can be transitioned.");

    public static readonly Error DuplicateRecipientTransaction =
        new("TopUp.DuplicateRecipientTransaction", "A transaction for this recipient in this run already exists.");

    public static readonly Error InvalidAccountTransactionReference =
        new("TopUp.InvalidAccountTransactionReference", "AccountTransactionId must be a valid positive reference.");

    public static readonly Error TransactionReasonRequired =
        new("TopUp.TransactionReasonRequired", "A safe display reason is required.");

    public static readonly Error CreditServiceUnavailable =
        new("TopUp.CreditServiceUnavailable", "Account credit service is temporarily unavailable.");

    public static readonly Error RecipientNotEligible =
        new("TopUp.RecipientNotEligible", "Recipient is not eligible for top-up credit.");

    public static readonly Error InvalidCreditAmount =
        new("TopUp.InvalidCreditAmount", "Credit amount must be positive.");
}
