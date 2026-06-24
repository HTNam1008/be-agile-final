namespace Moe.Modules.CourseBilling.IGateway.Payments;

public sealed record PayableCourseBill(
    long BillId,
    long CourseEnrollmentId,
    long CourseId,
    long PersonId,
    long OrganizationId,
    string CourseCode,
    string CourseName,
    decimal OutstandingAmount,
    string BillStatusCode);

public sealed record CourseBillingPlan(
    long CoursePaymentPlanId,
    long CourseId,
    string PlanTypeCode,
    int InstallmentCount,
    int IntervalMonths,
    bool IsActive);

public sealed record PayableStatement(
    long BillingStatementId,
    long PersonId,
    decimal OutstandingAmount,
    string CurrencyCode,
    IReadOnlyCollection<PayableStatementBill> Bills);

public sealed record PayableStatementBill(
    long BillingStatementItemId,
    long BillId,
    decimal OutstandingAmount,
    DateOnly CurrentDueDate,
    DateOnly OriginalDueDate,
    bool IsInstallment);

public sealed record BillPaymentAllocation(long BillId, decimal Amount);

public interface ICoursePaymentPlanGateway
{
    Task<CourseBillingPlan?> FindPlanAsync(
        long coursePaymentPlanId,
        CancellationToken cancellationToken);
}

public interface ICoursePaymentGateway
{
    Task<PayableCourseBill?> FindPayableBillAsync(
        long billId,
        long personId,
        CancellationToken cancellationToken);

    Task<long?> FindCourseOrganizationIdAsync(long courseId, CancellationToken cancellationToken);

    Task ApplySuccessfulPaymentAsync(
        long billId,
        decimal amount,
        bool paidInFull,
        DateTime paidAtUtc,
        CancellationToken cancellationToken);

    Task ApplyPaymentFailureAsync(long billId, CancellationToken cancellationToken);
    Task ApplyFullRefundAsync(long billId, DateTime refundedAtUtc, CancellationToken cancellationToken);
    Task ApplyFullRefundForBillsAsync(
        IReadOnlyCollection<long> billIds,
        DateTime refundedAtUtc,
        CancellationToken cancellationToken);

    Task<PayableStatement?> FindPayableStatementAsync(
        long statementId,
        long personId,
        CancellationToken cancellationToken);

    Task ApplyStatementPaymentAsync(
        long statementId,
        IReadOnlyCollection<BillPaymentAllocation> allocations,
        DateTime paidAtUtc,
        CancellationToken cancellationToken);

    Task DeferStatementAsync(
        long statementId,
        long personId,
        long failedPaymentId,
        IReadOnlyCollection<long> billIds,
        long actorLoginAccountId,
        DateTime utcNow,
        CancellationToken cancellationToken);

}
