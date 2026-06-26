namespace Moe.Modules.CourseBilling.Contracts.Enrollments;

public sealed record SelfJoinCourseRequest(
    long CourseId,
    long CoursePaymentPlanId,
    IReadOnlyCollection<long>? FasApplicationSchemeIds = null);
