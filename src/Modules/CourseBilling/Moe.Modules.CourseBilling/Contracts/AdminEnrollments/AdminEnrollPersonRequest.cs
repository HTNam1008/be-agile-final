namespace Moe.Modules.CourseBilling.Contracts.AdminEnrollments;

public sealed record AdminEnrollPersonRequest(string StudentNumber, long CoursePaymentPlanId);
