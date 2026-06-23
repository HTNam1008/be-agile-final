namespace Moe.Modules.CourseBilling.Contracts.BillingStatements;

public sealed record BillingStatementResponse(
    long BillingStatementId,
    int StatementYear,
    int StatementMonth,
    string CurrencyCode,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string StatementStatusCode,
    IReadOnlyCollection<BillingStatementItemResponse> Items);

public sealed record BillingStatementItemResponse(
    long BillingStatementItemId,
    long BillId,
    long CourseEnrollmentId,
    long CourseId,
    string CourseCode,
    string CourseName,
    int SequenceNumber,
    DateOnly OriginalDueDate,
    DateOnly CurrentDueDate,
    int DeferralCount,
    decimal OutstandingAmount,
    string BillStatusCode,
    bool IsInstallment);
