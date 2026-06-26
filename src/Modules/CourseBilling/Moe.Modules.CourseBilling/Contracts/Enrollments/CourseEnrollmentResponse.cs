namespace Moe.Modules.CourseBilling.Contracts.Enrollments;

public sealed record CourseEnrollmentResponse(
    long CourseEnrollmentId,
    long PersonId,
    long CourseId,
    string EnrollmentSourceCode,
    long EnrolledByLoginAccountId,
    string EnrollmentStatusCode,
    long? BillId,
    string? BillNumber,
    string? BillStatusCode,
    int BillLineCount,
    decimal GrossAmount,
    decimal NetPayableAmount,
    decimal OutstandingAmount,
    IReadOnlyCollection<GeneratedEnrollmentBillResponse> GeneratedBills);

public sealed record GeneratedEnrollmentBillResponse(
    long BillId,
    string BillNumber,
    int SequenceNumber,
    DateOnly CurrentDueDate,
    decimal NetPayableAmount,
    decimal OutstandingAmount,
    string BillStatusCode);

public sealed record PaymentPlanBillPreviewResponse(
    long CourseEnrollmentId,
    long CoursePaymentPlanId,
    string PlanTypeCode,
    int InstallmentCount,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetPayableAmount,
    IReadOnlyCollection<PaymentPlanPreviewBillResponse> Bills);

public sealed record PaymentPlanPreviewBillResponse(
    long BillId,
    string BillNumber,
    int SequenceNumber,
    DateOnly OriginalDueDate,
    DateOnly CurrentDueDate,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetPayableAmount,
    decimal OutstandingAmount,
    string BillStatusCode,
    string PlanTypeCode,
    bool IsInstallment,
    bool CanDefer,
    string? DeferBlockedReason,
    IReadOnlyCollection<PaymentPlanPreviewBillLineResponse> Lines);

public sealed record PaymentPlanPreviewBillLineResponse(
    long BillLineId,
    long FeeComponentId,
    long? CourseFeeId,
    string ComponentCode,
    string ComponentName,
    string ComponentTypeCode,
    string CalculationTypeCode,
    string Description,
    decimal Quantity,
    decimal UnitAmount,
    decimal GrossAmount,
    decimal SubsidyAmount,
    decimal NetAmount);
