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
    string? ProviderHostedInvoiceUrl,
    string? ProviderInvoicePdfUrl,
    string? ProviderReceiptUrl,
    DateTime InitiatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    IReadOnlyCollection<UserPaymentHistoryPartResponse> Parts,
    IReadOnlyCollection<UserPaymentHistoryRefundResponse> Refunds,
    IReadOnlyCollection<UserPaymentHistoryFasSettlementResponse> FasSettlements);

public sealed record PaymentReceiptResponse(
    long PaymentId,
    string PaymentNumber,
    string ReceiptNumber,
    DateTime IssuedAtUtc,
    string StatusCode,
    string StudentName,
    long? BillingStatementId,
    string CurrencyCode,
    decimal TotalPaidAmount,
    decimal EducationAccountAmount,
    decimal OnlinePaymentAmount,
    IReadOnlyCollection<PaymentReceiptItemResponse> Items,
    IReadOnlyCollection<PaymentReceiptPartResponse> PaymentParts,
    string? ProviderHostedInvoiceUrl,
    string? ProviderInvoicePdfUrl,
    string? ProviderReceiptUrl);

public sealed record PaymentReceiptItemResponse(
    long BillId,
    string? BillNumber,
    string? CourseCode,
    string? CourseName,
    int SequenceNumber,
    DateOnly DueDate,
    decimal PaidAmount,
    decimal OutstandingAmount);

public sealed record PaymentReceiptPartResponse(
    long PaymentPartId,
    string PaymentMethodCode,
    decimal Amount,
    string StatusCode,
    long? EducationAccountId,
    long? AccountTransactionId,
    string? ProviderCode,
    string? ProviderReference);

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
