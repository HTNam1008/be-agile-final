namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record UserPaymentHistoryResponse(
    long PaymentId,
    string PaymentNumber,
    long? BillingStatementId,
    decimal PaymentAmount,
    decimal SuccessfulAmount,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    string PaymentStatusCode,
    string? ReceiptNumber,
    DateTime InitiatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    IReadOnlyCollection<UserPaymentHistoryPartResponse> Parts,
    IReadOnlyCollection<UserPaymentHistoryRefundResponse> Refunds,
    IReadOnlyCollection<UserPaymentHistoryFasSettlementResponse> FasSettlements);

public sealed record UserPaymentHistoryPartResponse(
    long PaymentPartId,
    int SequenceNumber,
    string PaymentMethodCode,
    string FundingSourceCode,
    long? EducationAccountId,
    long? AccountTransactionId,
    decimal Amount,
    string? ProviderCode,
    string? ProviderReference,
    string PartStatusCode,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? SettledAtUtc,
    string? FailureReason);

public sealed record UserPaymentHistoryRefundResponse(
    long RefundId,
    long? PaymentPartId,
    string RefundMethodCode,
    decimal Amount,
    string RefundStatusCode,
    string? ProviderRefundId,
    long? AccountTransactionId,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? FailureReason);

public sealed record UserPaymentHistoryFasSettlementResponse(
    long FasVoucherRedemptionId,
    long FasApplicationSchemeId,
    long CourseId,
    long CourseEnrollmentId,
    long BillId,
    string? BillNumber,
    string? CourseCode,
    string? CourseName,
    string? SchemeName,
    decimal AppliedAmount,
    string StatusCode,
    DateTime CreatedAtUtc,
    DateTime? RedeemedAtUtc);
