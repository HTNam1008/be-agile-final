namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record StatementPaymentPreviewResponse(
    long BillingStatementId,
    decimal StatementOutstandingAmount,
    decimal EducationAccountCurrentBalance,
    decimal EducationAccountReservedAmount,
    decimal EducationAccountAvailableBalance,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string CurrencyCode,
    string RecommendedFundingOptionCode,
    IReadOnlyCollection<StatementFundingOptionResponse> FundingOptions);

public sealed record StatementFundingOptionResponse(
    string FundingOptionCode,
    string DisplayName,
    bool IsAvailable,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string? UnavailableReason);

public static class PaymentFundingOptionCodes
{
    public const string EducationAccountOnly = "EDUCATION_ACCOUNT_ONLY";
    public const string OnlineOnly = "ONLINE_ONLY";
    public const string EducationAccountThenOnline = "EDUCATION_ACCOUNT_THEN_ONLINE";
}

public sealed record PayBillingStatementRequest(
    string IdempotencyKey,
    string FundingOptionCode = PaymentFundingOptionCodes.EducationAccountThenOnline);

public sealed record PayBillingStatementResponse(
    long PaymentId,
    string PaymentStatusCode,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string? CheckoutUrl,
    long? PaymentCheckoutSessionId,
    DateTime? CheckoutExpiresAtUtc,
    bool Resumed);

public sealed record DeferBillingStatementRequest(long FailedPaymentId);
