namespace Moe.Modules.FasPayment.Contracts.Payments;

public sealed record OutstandingBillsResponse(
    decimal EducationAccountBalance,
    string CurrencyCode,
    IReadOnlyCollection<OutstandingBillDto> Bills);

public sealed record OutstandingBillDto(
    long BillId,
    string BillNumber,
    long CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    DateTime IssuedAtUtc,
    DateOnly DueDate,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetPayableAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string BillStatusCode,
    IReadOnlyCollection<OutstandingBillLineDto> Lines);

public sealed record OutstandingBillLineDto(
    long BillLineId,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetAmount);

public sealed record PayBillRequest(
    long BillId,
    string PaymentMethodCode,
    string? IdempotencyKey);

public sealed record PayBillResponse(
    long PaymentId,
    long PaymentPartId,
    long BillId,
    string BillNumber,
    string PaymentNumber,
    string ReceiptNumber,
    string PaymentMethodCode,
    decimal PaymentAmount,
    string PaymentStatusCode,
    decimal BillOutstandingAmount,
    string BillStatusCode,
    decimal? EducationAccountBalanceAfter);

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
