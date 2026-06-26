namespace Moe.Modules.CourseBilling.Contracts.Enrollments;

public sealed record ChangePaymentPlanRequest(
    long CoursePaymentPlanId,
    IReadOnlyCollection<long>? FasApplicationSchemeIds = null);
