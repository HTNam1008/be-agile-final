namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record StatementPaymentPreviewResponse(
    long BillingStatementId,
    decimal StatementOutstandingAmount,
    decimal EducationAccountCurrentBalance,
    decimal EducationAccountReservedAmount,
    decimal EducationAccountAvailableBalance,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string CurrencyCode);

public sealed record PayBillingStatementRequest(string IdempotencyKey);

public sealed record PayBillingStatementResponse(
    long PaymentId,
    string PaymentStatusCode,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string? CheckoutUrl);

public sealed record DeferBillingStatementRequest(long FailedPaymentId);
