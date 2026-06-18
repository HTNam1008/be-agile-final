namespace Moe.Modules.EducationAccountTopUp.Domain.TopUps;

public static class SafeReasons
{
    public const string AccountClosed = "Account is closed";
    public const string AccountPendingClosure = "Account is pending closure";
    public const string AccountNotActive = "Account is not active";
    public const string RecipientNotEligible = "Recipient is not eligible for this campaign";
    public const string DuplicateRecipient = "Recipient already processed in this run";

    public const string CreditServiceUnavailable = "Credit service temporarily unavailable";
    public const string CreditRejected = "Credit was rejected by account service";
    public const string InvalidAmount = "Top-up amount is not valid";
    public const string NonPositiveAmount = "Top-up amount must be positive";
    public const string TransientErrorExhaustedRetries = "Processing failed after maximum retries";
    public const string UnexpectedError = "An unexpected error occurred during processing";
}
